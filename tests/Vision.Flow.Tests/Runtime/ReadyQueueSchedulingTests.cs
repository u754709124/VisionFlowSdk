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
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // 就绪队列测试约束控制流边状态、汇聚和入口激活语义，避免调度器退化为递归路径遍历。
    internal static class ReadyQueueSchedulingTests
    {
        public static async Task SequentialReadyQueuePreservesDefinitionOrder()
        {
            var executionLog = new List<string>();
            var flow = CreateFlow("ready-queue-sequential", "A", "B", "C");
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "B"));
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "C"));
            AddManualEntry(flow, "A");

            var runner = CreateRecordingRunner(
                flow,
                executionLog,
                new InMemoryFlowEventSink(),
                new FlowExecutionOptions
                {
                    FanOutMode = FlowFanOutMode.Sequential,
                    MaxDegreeOfParallelism = 1
                });

            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "Sequential ready-queue execution should succeed.");
            AssertEx.SequenceEqual(new[] { "A", "B", "C" }, executionLog, "Sequential ready nodes should leave the queue in edge definition order.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task ParallelFanOutRunsBranchesConcurrently()
        {
            var probe = new ParallelReadyQueueProbe();
            var flow = CreateProbeFlow("ready-queue-parallel", "A", "B", "C");
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "B"));
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "C"));
            AddManualEntry(flow, "A");

            var registry = new NodeRegistry();
            registry.Register(new ParallelReadyQueueNodeFactory(probe));
            var runner = new FlowEngine(
                registry,
                new InMemoryFlowEventSink(),
                null,
                new FlowExecutionOptions
                {
                    FanOutMode = FlowFanOutMode.Parallel,
                    MaxDegreeOfParallelism = 2
                }).CreateRunner(flow);

            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "Parallel fan-out should succeed.");
            AssertEx.True(probe.BranchesOverlapped, "Both ready fan-out branches should be running before either branch completes.");
            AssertEx.Equal(1, probe.GetExecutionCount("B"), "Parallel branch B should execute once.");
            AssertEx.Equal(1, probe.GetExecutionCount("C"), "Parallel branch C should execute once.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task ParallelStopFlowCancelsSiblingBranch()
        {
            var probe = new ParallelFailureProbe();
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "ready-queue-parallel-failure",
                FlowName = "Parallel failure cancellation",
                Version = "2.0.0"
            };
            flow.Nodes.Add(CreateFailureProbeNode("A"));
            flow.Nodes.Add(CreateFailureProbeNode("B"));
            flow.Nodes.Add(CreateFailureProbeNode("C"));
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "B"));
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "C"));
            AddManualEntry(flow, "A");

            var registry = new NodeRegistry();
            registry.Register(new ParallelFailureNodeFactory(probe));
            var runner = new FlowEngine(
                registry,
                new InMemoryFlowEventSink(),
                null,
                new FlowExecutionOptions
                {
                    FanOutMode = FlowFanOutMode.Parallel,
                    MaxDegreeOfParallelism = 2
                }).CreateRunner(flow);

            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Failed, result.Status,
                "A StopFlow failure in one parallel branch should fail the FlowRun.");
            AssertEx.True(probe.SiblingCancelled,
                "A StopFlow failure should cancel and drain a cooperative sibling branch.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task FanInExecutesOnceAfterAllInboundEdgesResolve()
        {
            var executionLog = new List<string>();
            var flow = CreateFlow("ready-queue-fan-in", "A", "B", "C", "D");
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "B"));
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "C"));
            flow.Edges.Add(CreateEdge("B", FlowPortNames.Next, "D"));
            flow.Edges.Add(CreateEdge("C", FlowPortNames.Next, "D"));
            AddManualEntry(flow, "A");

            var runner = CreateRecordingRunner(flow, executionLog, new InMemoryFlowEventSink(), null);
            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "Reconverging flow should succeed.");
            AssertEx.Equal(1, executionLog.Count(x => x == "D"), "A fan-in node must execute exactly once per activation.");
            AssertEx.True(executionLog.IndexOf("D") > executionLog.IndexOf("B"), "Fan-in must wait until branch B resolves.");
            AssertEx.True(executionLog.IndexOf("D") > executionLog.IndexOf("C"), "Fan-in must wait until branch C resolves.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task UnselectedConditionalBranchPropagatesSkip()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var flow = CreateConditionalFlow("ready-queue-skip-selected", includeSelectedBranch: true);
            var runner = CreateConditionRunner(flow, executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "A selected condition branch should complete successfully.");
            AssertEx.SequenceEqual(new[] { "Selected", "Join" }, executionLog, "The unselected branch should be skipped while the selected path reaches the join.");
            AssertEx.False(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeStarted && string.Equals(x.NodeId, "Skipped", StringComparison.OrdinalIgnoreCase)),
                "An unselected branch node must not start.");
            AssertEx.Equal(
                1,
                sink.Events.Count(x => x.EventType == FlowRuntimeEventType.NodeSkipped && string.Equals(x.NodeId, "Skipped", StringComparison.OrdinalIgnoreCase)),
                "An unselected node should publish NodeSkipped exactly once.");
            AssertEx.Equal(1, executionLog.Count(x => x == "Join"), "The join should run once after its taken and skipped inbound edges resolve.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task AllInboundEdgesSkippedDoesNotExecuteNode()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var flow = CreateConditionalFlow("ready-queue-all-skipped", includeSelectedBranch: false);
            var runner = CreateConditionRunner(flow, executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "A flow with an entirely skipped subgraph should still complete successfully.");
            AssertEx.SequenceEqual(new[] { "Selected" }, executionLog, "Nodes whose inbound edges are all skipped must not execute.");
            AssertEx.False(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeStarted && string.Equals(x.NodeId, "AllSkipped", StringComparison.OrdinalIgnoreCase)),
                "The all-skipped fan-in node must not publish NodeStarted.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task EntryCanStartFromMiddleNode()
        {
            var executionLog = new List<string>();
            var flow = CreateFlow("ready-queue-middle-entry", "A", "B", "C");
            flow.Edges.Add(CreateEdge("A", FlowPortNames.Next, "B"));
            flow.Edges.Add(CreateEdge("B", FlowPortNames.Next, "C"));
            AddManualEntry(flow, "B");

            var runner = CreateRecordingRunner(flow, executionLog, new InMemoryFlowEventSink(), null);
            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", new FlowToken())).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "A middle-node entry should activate its target directly.");
            AssertEx.SequenceEqual(new[] { "B", "C" }, executionLog, "Nodes upstream of the selected entry must not execute.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static async Task NodeEventContinuationUsesReadyQueue()
        {
            var executionLog = new List<string>();
            var listenerFactory = new TestListenerNodeFactory();
            var registry = new NodeRegistry();
            registry.Register(listenerFactory);
            registry.Register(new RecordingNodeFactory(executionLog));

            var flow = new RuntimeFlowDefinition
            {
                FlowId = "ready-queue-node-event",
                FlowName = "NodeEvent Ready Queue",
                Version = "2.0.0"
            };
            flow.Nodes.Add(new NodeDefinition { Id = "Listener", Type = TestListenerNodeFactory.TypeName, Name = "Listener", Version = "1.0.0" });
            flow.Nodes.Add(CreateRecordingNode("B"));
            flow.Nodes.Add(CreateRecordingNode("C"));
            flow.Nodes.Add(CreateRecordingNode("D"));
            flow.Edges.Add(CreateEdge("Listener", "Frame", "B"));
            flow.Edges.Add(CreateEdge("Listener", "Frame", "C"));
            flow.Edges.Add(CreateEdge("B", FlowPortNames.Next, "D"));
            flow.Edges.Add(CreateEdge("C", FlowPortNames.Next, "D"));
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "FrameEntry",
                TriggerKind = FlowTriggerKind.NodeEvent,
                SourceNodeId = "Listener",
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

            var sink = new InMemoryFlowEventSink();
            var runner = new FlowEngine(registry, sink).CreateRunner(flow);
            await runner.StartAsync().ConfigureAwait(false);
            await listenerFactory.Instances["Listener"].EmitAsync("payload", "frame-001").ConfigureAwait(false);

            AssertEx.Equal(1, executionLog.Count(x => x == "B"), "NodeEvent branch B should execute once.");
            AssertEx.Equal(1, executionLog.Count(x => x == "C"), "NodeEvent branch C should execute once.");
            AssertEx.Equal(1, executionLog.Count(x => x == "D"), "NodeEvent fan-in should execute once.");
            AssertEx.True(executionLog.IndexOf("D") > executionLog.IndexOf("B"), "NodeEvent fan-in should wait for branch B.");
            AssertEx.True(executionLog.IndexOf("D") > executionLog.IndexOf("C"), "NodeEvent fan-in should wait for branch C.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.FlowRunCompleted), "NodeEvent continuation should complete through the unified run lifecycle.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        private static RuntimeFlowDefinition CreateFlow(string id, params string[] nodeIds)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = id,
                FlowName = id,
                Version = "2.0.0"
            };
            for (var index = 0; index < nodeIds.Length; index++)
            {
                flow.Nodes.Add(CreateRecordingNode(nodeIds[index]));
            }

            return flow;
        }

        private static RuntimeFlowDefinition CreateProbeFlow(string id, params string[] nodeIds)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = id,
                FlowName = id,
                Version = "2.0.0"
            };
            for (var index = 0; index < nodeIds.Length; index++)
            {
                flow.Nodes.Add(new NodeDefinition
                {
                    Id = nodeIds[index],
                    Type = ParallelReadyQueueNodeFactory.TypeName,
                    Name = nodeIds[index],
                    Version = "1.0.0"
                });
            }

            return flow;
        }

        private static RuntimeFlowDefinition CreateConditionalFlow(string id, bool includeSelectedBranch)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = id,
                FlowName = id,
                Version = "2.0.0"
            };
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "Condition",
                Type = FlowNodeTypes.ConditionIf,
                Name = "Condition",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.LeftBinding, NodeSettingValue.ForConstant("selected") },
                    { FlowSettingNames.Operator, NodeSettingValue.ForConstant(ConditionOperator.Equal) },
                    { FlowSettingNames.RightValue, NodeSettingValue.ForConstant("selected") }
                }
            });
            flow.Nodes.Add(CreateRecordingNode("Selected"));

            if (includeSelectedBranch)
            {
                flow.Nodes.Add(CreateRecordingNode("Skipped"));
                flow.Nodes.Add(CreateRecordingNode("Join"));
                flow.Edges.Add(CreateEdge("Condition", FlowPortNames.True, "Selected"));
                flow.Edges.Add(CreateEdge("Condition", FlowPortNames.False, "Skipped"));
                flow.Edges.Add(CreateEdge("Selected", FlowPortNames.Next, "Join"));
                flow.Edges.Add(CreateEdge("Skipped", FlowPortNames.Next, "Join"));
            }
            else
            {
                flow.Nodes.Add(CreateRecordingNode("SkippedA"));
                flow.Nodes.Add(CreateRecordingNode("SkippedB"));
                flow.Nodes.Add(CreateRecordingNode("AllSkipped"));
                flow.Edges.Add(CreateEdge("Condition", FlowPortNames.True, "Selected"));
                flow.Edges.Add(CreateEdge("Condition", FlowPortNames.False, "SkippedA"));
                flow.Edges.Add(CreateEdge("Condition", FlowPortNames.False, "SkippedB"));
                flow.Edges.Add(CreateEdge("SkippedA", FlowPortNames.Next, "AllSkipped"));
                flow.Edges.Add(CreateEdge("SkippedB", FlowPortNames.Next, "AllSkipped"));
            }

            AddManualEntry(flow, "Condition");
            return flow;
        }

        private static IFlowRunner CreateRecordingRunner(
            RuntimeFlowDefinition flow,
            IList<string> executionLog,
            InMemoryFlowEventSink sink,
            FlowExecutionOptions options)
        {
            var registry = new NodeRegistry();
            registry.Register(new RecordingNodeFactory(executionLog));
            return new FlowEngine(registry, sink, null, options).CreateRunner(flow);
        }

        private static IFlowRunner CreateConditionRunner(
            RuntimeFlowDefinition flow,
            IList<string> executionLog,
            InMemoryFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new RecordingNodeFactory(executionLog));
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static NodeDefinition CreateRecordingNode(string id)
        {
            return new NodeDefinition
            {
                Id = id,
                Type = RecordingNodeFactory.TypeName,
                Name = id,
                Version = "1.0.0"
            };
        }

        private static NodeDefinition CreateFailureProbeNode(string id)
        {
            return new NodeDefinition
            {
                Id = id,
                Type = ParallelFailureNodeFactory.TypeName,
                Name = id,
                Version = "1.0.0"
            };
        }

        private static EdgeDefinition CreateEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            return new EdgeDefinition
            {
                FromNodeId = fromNodeId,
                FromPort = fromPort,
                ToNodeId = toNodeId,
                ToPort = FlowPortNames.In
            };
        }

        private static void AddManualEntry(RuntimeFlowDefinition flow, string targetNodeId)
        {
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TriggerKind = FlowTriggerKind.Manual,
                TargetNodeId = targetNodeId
            });
        }
    }

    internal sealed class ParallelFailureNodeFactory : INodeFactory
    {
        public const string TypeName = "test.ready-queue-parallel-failure";
        private readonly ParallelFailureProbe _probe;

        public ParallelFailureNodeFactory(ParallelFailureProbe probe)
        {
            _probe = probe;
        }

        public string NodeType { get { return TypeName; } }

        public NodeDescriptor Descriptor
        {
            get
            {
                return new NodeDescriptor
                {
                    NodeType = TypeName,
                    DisplayName = "并行失败取消探针",
                    Category = "测试",
                    Version = "1.0.0"
                };
            }
        }

        public IFlowNode Create(NodeDefinition definition)
        {
            return new ParallelFailureNode(definition.Id, _probe);
        }
    }

    internal sealed class ParallelFailureNode : IFlowNode
    {
        private readonly string _nodeId;
        private readonly ParallelFailureProbe _probe;

        public ParallelFailureNode(string nodeId, ParallelFailureProbe probe)
        {
            _nodeId = nodeId;
            _probe = probe;
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            if (string.Equals(_nodeId, "B", StringComparison.OrdinalIgnoreCase))
            {
                return _probe.FailAfterSiblingStartsAsync(cancellationToken);
            }

            if (string.Equals(_nodeId, "C", StringComparison.OrdinalIgnoreCase))
            {
                return _probe.WaitForCancellationAsync(cancellationToken);
            }

            return Task.FromResult(NodeExecutionResult.Success(FlowPortNames.Next));
        }
    }

    internal sealed class ParallelFailureProbe
    {
        private readonly TaskCompletionSource<bool> _siblingStarted =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool SiblingCancelled { get; private set; }

        public async Task<NodeExecutionResult> FailAfterSiblingStartsAsync(CancellationToken cancellationToken)
        {
            await _siblingStarted.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return NodeExecutionResult.Failure("parallel branch failed");
        }

        public async Task<NodeExecutionResult> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            _siblingStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return NodeExecutionResult.Success();
            }
            catch (OperationCanceledException)
            {
                SiblingCancelled = true;
                throw;
            }
        }
    }

    internal sealed class ParallelReadyQueueNodeFactory : INodeFactory
    {
        public const string TypeName = "test.ready-queue-parallel";
        private readonly ParallelReadyQueueProbe _probe;

        public ParallelReadyQueueNodeFactory(ParallelReadyQueueProbe probe)
        {
            _probe = probe;
        }

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
                    DisplayName = "并行就绪队列探针",
                    Category = "测试",
                    Version = "1.0.0"
                };
            }
        }

        public IFlowNode Create(NodeDefinition definition)
        {
            return new ParallelReadyQueueNode(definition.Id, _probe);
        }
    }

    internal sealed class ParallelReadyQueueNode : IFlowNode
    {
        private readonly string _nodeId;
        private readonly ParallelReadyQueueProbe _probe;

        public ParallelReadyQueueNode(string nodeId, ParallelReadyQueueProbe probe)
        {
            _nodeId = nodeId;
            _probe = probe;
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            if (string.Equals(_nodeId, "B", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_nodeId, "C", StringComparison.OrdinalIgnoreCase))
            {
                await _probe.EnterBranchAsync(_nodeId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _probe.RecordExecution(_nodeId);
            }

            return NodeExecutionResult.Success(FlowPortNames.Next);
        }
    }

    internal sealed class ParallelReadyQueueProbe
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, int> _executionCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly TaskCompletionSource<object> _bothBranchesStarted = new TaskCompletionSource<object>();
        private int _activeBranches;

        public bool BranchesOverlapped { get; private set; }

        public void RecordExecution(string nodeId)
        {
            lock (_gate)
            {
                int count;
                _executionCounts.TryGetValue(nodeId, out count);
                _executionCounts[nodeId] = count + 1;
            }
        }

        public int GetExecutionCount(string nodeId)
        {
            lock (_gate)
            {
                int count;
                return _executionCounts.TryGetValue(nodeId, out count) ? count : 0;
            }
        }

        public async Task EnterBranchAsync(string nodeId, CancellationToken cancellationToken)
        {
            RecordExecution(nodeId);
            lock (_gate)
            {
                _activeBranches++;
                if (_activeBranches >= 2)
                {
                    BranchesOverlapped = true;
                    _bothBranchesStarted.TrySetResult(null);
                }
            }

            try
            {
                var timeout = Task.Delay(2000, cancellationToken);
                var completed = await Task.WhenAny(_bothBranchesStarted.Task, timeout).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (completed != _bothBranchesStarted.Task)
                {
                    throw new InvalidOperationException("Parallel fan-out branches did not overlap within the test timeout.");
                }

                await _bothBranchesStarted.Task.ConfigureAwait(false);
            }
            finally
            {
                lock (_gate)
                {
                    _activeBranches--;
                }
            }
        }
    }
}
