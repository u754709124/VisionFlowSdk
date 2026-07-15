using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    internal static class FlowTriggerTests
    {
        public static async Task ExternalTriggerUsesDeclaredInputs()
        {
            var sink = new InMemoryFlowEventSink();
            var runner = CreateCommonRunner(CreateExternalInputFlow(), sink);
            await runner.StartAsync().ConfigureAwait(false);

            var result = await runner.TriggerAsync(
                new FlowTriggerRequest
                {
                    EntryName = "ExternalStart",
                    Source = FlowTriggerSource.External,
                    Inputs = new Dictionary<string, object> { { "number", "42" } }
                }).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "A valid external trigger should succeed.");
            AssertEx.Equal(42, Convert.ToInt32(result.Variables["Captured"]), "TriggerInput should resolve into an editable node setting.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.FlowRunStarted && x.FlowRunId == result.FlowRunId), "FlowRunStarted should use the returned FlowRunId.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.FlowRunCompleted && x.FlowRunId == result.FlowRunId), "FlowRunCompleted should use the returned FlowRunId.");
            var tokenCreated = sink.Events.First(x => x.EventType == FlowRuntimeEventType.TokenCreated && x.FlowRunId == result.FlowRunId);
            var tokenInputs = (IDictionary<string, object>)tokenCreated.Data[FlowRuntimeDataKeys.TriggerInputs];
            AssertEx.Equal(42, Convert.ToInt32(tokenInputs["number"]), "TokenCreated should carry the normalized TriggerInputs for the same run.");

            var missing = await runner.TriggerAsync(
                new FlowTriggerRequest
                {
                    EntryName = "ExternalStart",
                    Source = FlowTriggerSource.External
                }).ConfigureAwait(false);
            AssertEx.Equal(FlowRunStatus.Rejected, missing.Status, "Missing required inputs should reject the trigger.");
            AssertEx.Equal(0, missing.Variables.Count, "Rejected triggers should not expose a variable snapshot.");

            var wrongType = await runner.TriggerAsync(
                new FlowTriggerRequest
                {
                    EntryName = "ExternalStart",
                    Source = FlowTriggerSource.External,
                    Inputs = new Dictionary<string, object> { { "number", "not-a-number" } }
                }).ConfigureAwait(false);
            AssertEx.Equal(FlowRunStatus.Rejected, wrongType.Status, "Invalid input types should reject the trigger.");

            var wrongSource = await runner.TriggerAsync(
                new FlowTriggerRequest
                {
                    EntryName = "ExternalStart",
                    Source = FlowTriggerSource.Manual,
                    Inputs = new Dictionary<string, object> { { "number", 1 } }
                }).ConfigureAwait(false);
            AssertEx.Equal(FlowRunStatus.Rejected, wrongSource.Status, "The request source must match the entry TriggerKind.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task EntryQueueRejectsWhenFull()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "entry-gate",
                FlowName = "Entry Gate",
                Version = "2.0.0"
            };
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "delay",
                Type = FlowNodeTypes.DelayWait,
                Name = "Delay",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.DelayMs, NodeSettingValue.ForConstant(250) }
                }
            });
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "delay",
                TriggerKind = FlowTriggerKind.Manual,
                ExecutionPolicy = new TriggerExecutionPolicy
                {
                    MaxConcurrentRuns = 1,
                    QueueCapacity = 0,
                    QueueFullBehavior = TriggerQueueFullBehavior.Reject
                }
            });

            var sink = new InMemoryFlowEventSink();
            var runner = CreateCommonRunner(flow, sink);
            await runner.StartAsync().ConfigureAwait(false);
            var firstTask = runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken()));
            await WaitForEventAsync(sink, FlowRuntimeEventType.NodeStarted, "delay").ConfigureAwait(false);
            var second = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);
            var first = await firstTask.ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, first.Status, "The active run should complete normally.");
            AssertEx.Equal(FlowRunStatus.Rejected, second.Status, "QueueCapacity zero should reject a concurrent trigger.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.FlowRunRejected && x.FlowRunId == second.FlowRunId), "A rejected trigger should publish FlowRunRejected.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task EntryConcurrencyPolicyControlsSerialAndParallelRuns()
        {
            var serialFactory = new ConcurrencyProbeNodeFactory();
            var serialRunner = CreateProbeRunner(CreateProbeFlow(null), serialFactory);
            await serialRunner.StartAsync().ConfigureAwait(false);
            var serialResults = await Task.WhenAll(
                serialRunner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())),
                serialRunner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken()))).ConfigureAwait(false);

            AssertEx.True(serialResults.All(x => x.Status == FlowRunStatus.Succeeded), "Queued serial triggers should both complete successfully.");
            AssertEx.Equal(1, serialFactory.Probe.MaxActiveCount, "MaxConcurrentRuns one should keep the entry serial.");
            await serialRunner.StopAsync().ConfigureAwait(false);

            var parallelFactory = new ConcurrencyProbeNodeFactory();
            var parallelRunner = CreateProbeRunner(CreateProbeFlow(2), parallelFactory);
            await parallelRunner.StartAsync().ConfigureAwait(false);
            var parallelResults = await Task.WhenAll(
                parallelRunner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())),
                parallelRunner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken()))).ConfigureAwait(false);

            AssertEx.True(parallelResults.All(x => x.Status == FlowRunStatus.Succeeded), "Parallel entry triggers should both complete successfully.");
            AssertEx.Equal(2, parallelFactory.Probe.MaxActiveCount, "MaxConcurrentRuns two should allow two runs to execute together.");
            await parallelRunner.StopAsync().ConfigureAwait(false);
        }

        public static async Task NodeEventStartsOnlyReferencedListener()
        {
            var listenerFactory = new TestListenerNodeFactory();
            var registry = new NodeRegistry();
            registry.Register(listenerFactory);
            CommonNodeRegistration.RegisterAll(registry);

            var flow = CreateNodeEventFlow();
            var sink = new InMemoryFlowEventSink();
            var runner = new FlowEngine(registry, sink).CreateRunner(flow);
            await runner.StartAsync().ConfigureAwait(false);

            AssertEx.True(listenerFactory.Instances.ContainsKey("listener1"), "The NodeEvent source listener should be created.");
            AssertEx.False(listenerFactory.Instances.ContainsKey("listener2"), "Unreferenced listener nodes must not be started.");
            var listener = listenerFactory.Instances["listener1"];
            AssertEx.Equal(1, listener.StartCount, "The referenced listener should start once.");
            AssertEx.Equal("FrameEntry", listener.Context.Entry.EntryName, "Listener context should expose its NodeEvent entry.");

            await listener.EmitAsync("payload", "frame-001").ConfigureAwait(false);
            var completed = sink.Events.LastOrDefault(x => x.EventType == FlowRuntimeEventType.FlowRunCompleted);
            AssertEx.NotNull(completed, "A listener continuation should complete a flow run.");
            AssertEx.Equal("NodeEvent", Convert.ToString(completed.Data[FlowRuntimeDataKeys.TriggerSource]), "NodeEvent lifecycle events should identify their trigger source.");
            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.OutputProduced &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), "set.Value", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.Value]), "frame-001", StringComparison.Ordinal)),
                "NodeEvent trigger inputs should be available to downstream node settings.");

            await runner.StopAsync().ConfigureAwait(false);
            AssertEx.Equal(1, listener.StopCount, "The started listener should stop once.");
        }

        public static Task TriggerInputSelectorsAreValidated()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var validator = new FlowValidator(registry);

            var valid = validator.Validate(CreateExternalInputFlow());
            AssertEx.True(valid.IsValid, "A reachable declared TriggerInput selector should publish successfully.");

            var unavailableFlow = CreateExternalInputFlow();
            unavailableFlow.Nodes[0].Settings[FlowSettingNames.Value] = NodeSettingValue.ForVariable(VariableSelector.ForTriggerInput("missing"));
            var unavailable = validator.Validate(unavailableFlow);
            AssertEx.True(unavailable.Errors.Any(x => x.Code == FlowValidationIssueCodes.TriggerInputUnavailable), "An undeclared TriggerInput selector should be rejected.");

            var conflictFlow = CreateExternalInputFlow();
            conflictFlow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "set",
                TriggerKind = FlowTriggerKind.Manual,
                Inputs =
                {
                    new TriggerInputDescriptor
                    {
                        Name = "number",
                        DataType = FlowDataType.String
                    }
                }
            });
            var conflict = validator.Validate(conflictFlow);
            AssertEx.True(conflict.Errors.Any(x => x.Code == FlowValidationIssueCodes.TriggerInputTypeConflict), "Reachable entries must not declare the same input with conflicting types.");

            var invalidSchemaFlow = CreateExternalInputFlow();
            invalidSchemaFlow.Entries[0].Inputs[0].DefaultValue = "invalid-integer";
            invalidSchemaFlow.Entries[0].ExecutionPolicy.MaxConcurrentRuns = 0;
            var invalidSchema = validator.Validate(invalidSchemaFlow);
            AssertEx.True(invalidSchema.Errors.Any(x => x.Code == FlowValidationIssueCodes.EntryInputDefaultInvalid), "Trigger input defaults should match their declared type.");
            AssertEx.True(invalidSchema.Errors.Any(x => x.Code == FlowValidationIssueCodes.EntryExecutionPolicyInvalid), "Entry execution policies should be validated.");
            return Task.FromResult(0);
        }

        private static RuntimeFlowDefinition CreateExternalInputFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "external-input",
                FlowName = "External Input",
                Version = "2.0.0"
            };
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "set",
                Type = FlowNodeTypes.VariableSet,
                Name = "Set Variable",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.VariableName, NodeSettingValue.ForConstant("Captured") },
                    { FlowSettingNames.Value, NodeSettingValue.ForVariable(VariableSelector.ForTriggerInput("number")) }
                }
            });
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ExternalStart",
                TargetNodeId = "set",
                TriggerKind = FlowTriggerKind.External,
                Inputs =
                {
                    new TriggerInputDescriptor
                    {
                        Name = "number",
                        DisplayName = "数值",
                        DataType = FlowDataType.Int32,
                        IsRequired = true
                    }
                }
            });
            return flow;
        }

        private static RuntimeFlowDefinition CreateNodeEventFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "node-event",
                FlowName = "Node Event",
                Version = "2.0.0"
            };
            flow.Nodes.Add(new NodeDefinition { Id = "listener1", Type = TestListenerNodeFactory.TypeName, Name = "Listener 1", Version = "1.0.0" });
            flow.Nodes.Add(new NodeDefinition { Id = "listener2", Type = TestListenerNodeFactory.TypeName, Name = "Listener 2", Version = "1.0.0" });
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "set",
                Type = FlowNodeTypes.VariableSet,
                Name = "Set Variable",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.VariableName, NodeSettingValue.ForConstant("Captured") },
                    { FlowSettingNames.Value, NodeSettingValue.ForVariable(VariableSelector.ForTriggerInput("payload")) }
                }
            });
            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "listener1",
                FromPort = "Frame",
                ToNodeId = "set",
                ToPort = FlowPortNames.In
            });
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "FrameEntry",
                TriggerKind = FlowTriggerKind.NodeEvent,
                SourceNodeId = "listener1",
                Inputs =
                {
                    new TriggerInputDescriptor
                    {
                        Name = "payload",
                        DataType = FlowDataType.String,
                        IsRequired = true
                    }
                }
            });
            return flow;
        }

        private static IFlowRunner CreateCommonRunner(RuntimeFlowDefinition flow, InMemoryFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateProbeFlow(int? maxConcurrentRuns)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "concurrency-" + (maxConcurrentRuns.HasValue ? maxConcurrentRuns.Value.ToString() : "default"),
                FlowName = "Concurrency Probe",
                Version = "2.0.0"
            };
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "probe",
                Type = ConcurrencyProbeNodeFactory.TypeName,
                Name = "Probe",
                Version = "1.0.0"
            });
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "probe",
                TriggerKind = FlowTriggerKind.Manual,
                ExecutionPolicy = maxConcurrentRuns.HasValue
                    ? new TriggerExecutionPolicy
                    {
                        MaxConcurrentRuns = maxConcurrentRuns.Value,
                        QueueCapacity = 64,
                        QueueFullBehavior = TriggerQueueFullBehavior.Reject
                    }
                    : new TriggerExecutionPolicy()
            });
            return flow;
        }

        private static IFlowRunner CreateProbeRunner(RuntimeFlowDefinition flow, ConcurrencyProbeNodeFactory factory)
        {
            var registry = new NodeRegistry();
            registry.Register(factory);
            return new FlowEngine(registry, new InMemoryFlowEventSink()).CreateRunner(flow);
        }

        private static async Task WaitForEventAsync(InMemoryFlowEventSink sink, FlowRuntimeEventType eventType, string nodeId)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (sink.Events.Any(x => x.EventType == eventType && string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for event " + eventType + " on node " + nodeId + ".");
        }
    }

    internal sealed class TestListenerNodeFactory : INodeFactory
    {
        public const string TypeName = "test.listener";

        public TestListenerNodeFactory()
        {
            Instances = new Dictionary<string, TestListenerNode>(StringComparer.OrdinalIgnoreCase);
        }

        public IDictionary<string, TestListenerNode> Instances { get; private set; }

        public string NodeType
        {
            get { return TypeName; }
        }

        public NodeDescriptor Descriptor
        {
            get
            {
                return new NodeDescriptor
                {
                    NodeType = TypeName,
                    DisplayName = "测试监听器",
                    Category = "测试",
                    Version = "1.0.0",
                    OutputPorts =
                    {
                        new NodePortDescriptor
                        {
                            Name = "Frame",
                            DisplayName = "帧事件",
                            Direction = FlowPortDirection.Output,
                            DataType = FlowDataType.Control
                        }
                    }
                };
            }
        }

        public IFlowNode Create(NodeDefinition definition)
        {
            var node = new TestListenerNode(definition.Id);
            Instances[definition.Id] = node;
            return node;
        }
    }

    internal sealed class TestListenerNode : IFlowListenerNode
    {
        private readonly string _nodeId;

        public TestListenerNode(string nodeId)
        {
            _nodeId = nodeId;
        }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public FlowListenerContext Context { get; private set; }

        public Task StartAsync(FlowListenerContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Context = context;
            StartCount++;
            return Task.FromResult(0);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            return Task.FromResult(0);
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(NodeExecutionResult.Success(FlowPortNames.Next));
        }

        public Task EmitAsync(string inputName, object value)
        {
            return Context.Continuations.DispatchAsync(
                new FlowContinuation
                {
                    SourceNodeId = _nodeId,
                    OutputPort = "Frame",
                    TriggerInputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        { inputName, value }
                    }
                },
                CancellationToken.None);
        }
    }

    internal sealed class ConcurrencyProbeNodeFactory : INodeFactory
    {
        public const string TypeName = "test.concurrency-probe";

        public ConcurrencyProbeNodeFactory()
        {
            Probe = new ConcurrencyProbe();
        }

        public ConcurrencyProbe Probe { get; private set; }

        public string NodeType
        {
            get { return TypeName; }
        }

        public NodeDescriptor Descriptor
        {
            get
            {
                return new NodeDescriptor
                {
                    NodeType = TypeName,
                    DisplayName = "并发探针",
                    Category = "测试",
                    Version = "1.0.0"
                };
            }
        }

        public IFlowNode Create(NodeDefinition definition)
        {
            return new ConcurrencyProbeNode(Probe);
        }
    }

    internal sealed class ConcurrencyProbeNode : IFlowNode
    {
        private readonly ConcurrencyProbe _probe;

        public ConcurrencyProbeNode(ConcurrencyProbe probe)
        {
            _probe = probe;
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            _probe.Enter();
            try
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                return NodeExecutionResult.Success(FlowPortNames.Next);
            }
            finally
            {
                _probe.Exit();
            }
        }
    }

    internal sealed class ConcurrencyProbe
    {
        private int _activeCount;
        private int _maxActiveCount;

        public int MaxActiveCount
        {
            get { return Volatile.Read(ref _maxActiveCount); }
        }

        public void Enter()
        {
            var active = Interlocked.Increment(ref _activeCount);
            while (true)
            {
                var currentMax = Volatile.Read(ref _maxActiveCount);
                if (active <= currentMax || Interlocked.CompareExchange(ref _maxActiveCount, active, currentMax) == currentMax)
                {
                    return;
                }
            }
        }

        public void Exit()
        {
            Interlocked.Decrement(ref _activeCount);
        }
    }
}
