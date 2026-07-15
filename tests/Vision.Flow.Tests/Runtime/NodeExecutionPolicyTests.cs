using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Vision.Flow.Tests
{
    internal static class NodeExecutionPolicyTests
    {
        public static async Task RetryDisabledExecutesOnce()
        {
            var harness = CreateHarness((attempt, context, cancellationToken) =>
                Task.FromResult(NodeExecutionResult.Failure("failure")));

            var result = await RunAsync(harness).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Failed, result.Status, "StopFlow should fail the run.");
            AssertEx.Equal(1, harness.Factory.AttemptCount, "Retry is disabled by default, so the node should execute once.");
            AssertEx.False(harness.Sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeRetrying),
                "Retry-disabled nodes should not publish NodeRetrying.");
        }

        public static async Task RetryCountAndIntervalAreApplied()
        {
            var harness = CreateHarness((attempt, context, cancellationToken) =>
                Task.FromResult(NodeExecutionResult.Failure("failure")));
            harness.Node.ExecutionPolicy.RetryPolicy.Enabled = true;
            harness.Node.ExecutionPolicy.RetryPolicy.MaxRetries = 2;
            harness.Node.ExecutionPolicy.RetryPolicy.RetryIntervalMs = 30;
            var stopwatch = Stopwatch.StartNew();

            var result = await RunAsync(harness).ConfigureAwait(false);
            stopwatch.Stop();

            AssertEx.Equal(FlowRunStatus.Failed, result.Status, "Exhausted retries should use StopFlow by default.");
            AssertEx.Equal(3, harness.Factory.AttemptCount, "MaxRetries excludes the first execution.");
            AssertEx.Equal(2, harness.Sink.Events.Count(x => x.EventType == FlowRuntimeEventType.NodeRetrying),
                "Every scheduled retry should publish NodeRetrying.");
            AssertEx.True(stopwatch.ElapsedMilliseconds >= 45,
                "Two fixed 30 ms retry intervals should contribute observable waiting time.");
            AssertEx.SequenceEqual(
                new[] { 2, 3 },
                harness.Sink.Events
                    .Where(x => x.EventType == FlowRuntimeEventType.NodeRetrying)
                    .Select(x => Convert.ToInt32(x.Data[FlowRuntimeDataKeys.Attempt])),
                "Retry events should identify the next attempt.");
        }

        public static async Task RetryCanRecoverNode()
        {
            var harness = CreateHarness((attempt, context, cancellationToken) =>
                Task.FromResult(attempt < 3
                    ? NodeExecutionResult.Failure("transient")
                    : NodeExecutionResult.Success(FlowPortNames.Next, new Dictionary<string, object> { { "Value", "recovered" } })));
            harness.Node.ExecutionPolicy.RetryPolicy.Enabled = true;
            harness.Node.ExecutionPolicy.RetryPolicy.MaxRetries = 2;
            harness.Node.ExecutionPolicy.RetryPolicy.RetryIntervalMs = 0;

            var result = await RunAsync(harness).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "A successful retry should recover the run.");
            AssertEx.Equal(3, harness.Factory.AttemptCount, "The third attempt should succeed.");
            AssertEx.Equal("recovered", Convert.ToString(result.Variables["probe.Value"]),
                "Outputs from the recovered attempt should be written.");
            AssertEx.True(harness.Sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.NodeRecovered &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.FailureStrategy]), "Retry", StringComparison.Ordinal)),
                "Retry recovery should publish NodeRecovered.");
        }

        public static async Task StopFlowIsTheDefaultFailureStrategy()
        {
            var harness = CreateHarness((attempt, context, cancellationToken) =>
                Task.FromResult(NodeExecutionResult.Failure("permanent")));

            var result = await RunAsync(harness).ConfigureAwait(false);

            AssertEx.Equal(FailureStrategy.StopFlow, harness.Node.ExecutionPolicy.FailureStrategy,
                "All nodes should default to StopFlow.");
            AssertEx.Equal(FlowRunStatus.Failed, result.Status, "StopFlow should terminate the current run.");
            AssertEx.True((result.ErrorMessage ?? string.Empty).IndexOf("probe", StringComparison.OrdinalIgnoreCase) >= 0,
                "StopFlow diagnostics should identify the failing node.");
        }

        public static async Task ErrorBranchContinuesThroughConnectedPort()
        {
            var executionLog = new List<string>();
            var harness = CreateHarness((attempt, context, cancellationToken) =>
            {
                lock (executionLog)
                {
                    executionLog.Add(context.Node.Id);
                }

                return Task.FromResult(string.Equals(context.Node.Id, "probe", StringComparison.OrdinalIgnoreCase)
                    ? NodeExecutionResult.Failure("handled")
                    : NodeExecutionResult.Success());
            });
            harness.Node.ExecutionPolicy.FailureStrategy = FailureStrategy.ErrorBranch;
            harness.Flow.Nodes.Add(CreateNode("handler"));
            harness.Flow.Edges.Add(CreateEdge("probe", FlowPortNames.Error, "handler"));

            var result = await RunAsync(harness).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "A connected ErrorBranch should handle the failure.");
            AssertEx.SequenceEqual(new[] { "probe", "handler" }, executionLog,
                "ErrorBranch should continue through the failure result port.");
            AssertEx.True(harness.Sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.NodeRecovered &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.FailureStrategy]), "ErrorBranch", StringComparison.Ordinal)),
                "Handled error branches should publish NodeRecovered.");
        }

        public static async Task DefaultOutputsContinueThroughNext()
        {
            var consumerSawFallback = false;
            var harness = CreateHarness((attempt, context, cancellationToken) =>
            {
                if (string.Equals(context.Node.Id, "probe", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(NodeExecutionResult.Failure("fallback"));
                }

                object value;
                consumerSawFallback = context.Variables.TryGet("probe.Value", out value) &&
                    string.Equals(Convert.ToString(value), "default-value", StringComparison.Ordinal);
                return Task.FromResult(NodeExecutionResult.Success());
            });
            harness.Node.ExecutionPolicy.FailureStrategy = FailureStrategy.DefaultOutputs;
            harness.Node.ExecutionPolicy.DefaultOutputs["Value"] = "default-value";
            harness.Flow.Nodes.Add(CreateNode("consumer"));
            harness.Flow.Edges.Add(CreateEdge("probe", FlowPortNames.Next, "consumer"));

            var result = await RunAsync(harness).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status, "DefaultOutputs should recover and continue through Next.");
            AssertEx.True(consumerSawFallback, "Downstream nodes should read namespaced default outputs from the variable pool.");
            AssertEx.Equal("default-value", Convert.ToString(result.Variables["probe.Value"]),
                "FlowRunResult should include default outputs.");
        }

        public static async Task TimeoutParticipatesInRetry()
        {
            var harness = CreateHarness(async (attempt, context, cancellationToken) =>
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                return NodeExecutionResult.Success();
            });
            harness.Node.ExecutionPolicy.TimeoutMs = 20;
            harness.Node.ExecutionPolicy.RetryPolicy.Enabled = true;
            harness.Node.ExecutionPolicy.RetryPolicy.MaxRetries = 1;
            harness.Node.ExecutionPolicy.RetryPolicy.RetryIntervalMs = 0;

            var result = await RunAsync(harness).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Failed, result.Status, "Exhausted timeout retries should stop the flow.");
            AssertEx.Equal(2, harness.Factory.AttemptCount, "Timeout is a retryable failure.");
            var timeoutEvent = harness.Sink.Events.LastOrDefault(x => x.EventType == FlowRuntimeEventType.NodeTimeout);
            AssertEx.NotNull(timeoutEvent, "The final timeout should publish NodeTimeout.");
            AssertEx.Equal("Timeout", Convert.ToString(timeoutEvent.Data[FlowRuntimeDataKeys.FailureKind]),
                "Timeout events should expose their stable failure classification.");
        }

        public static async Task BindingAndConfigurationFailuresDoNotRetry()
        {
            var bindingHarness = CreateHarness((attempt, context, cancellationToken) =>
            {
                throw new SettingBindingException("binding");
            });
            EnableManyRetries(bindingHarness.Node);
            var bindingResult = await RunAsync(bindingHarness).ConfigureAwait(false);

            var configurationHarness = CreateHarness((attempt, context, cancellationToken) =>
            {
                throw new NodeConfigurationException("configuration");
            });
            EnableManyRetries(configurationHarness.Node);
            var configurationResult = await RunAsync(configurationHarness).ConfigureAwait(false);

            AssertEx.Equal(FlowRunStatus.Failed, bindingResult.Status, "Binding failures should stop the flow.");
            AssertEx.Equal(1, bindingHarness.Factory.AttemptCount, "Binding failures must not be retried.");
            AssertEx.Equal("Binding", ReadFinalFailureKind(bindingHarness.Sink), "Binding failure classification should be stable.");
            AssertEx.Equal(FlowRunStatus.Failed, configurationResult.Status, "Configuration failures should stop the flow.");
            AssertEx.Equal(1, configurationHarness.Factory.AttemptCount, "Configuration failures must not be retried.");
            AssertEx.Equal("Configuration", ReadFinalFailureKind(configurationHarness.Sink),
                "Configuration failure classification should be stable.");
        }

        public static async Task CancellationInterruptsRetryInterval()
        {
            var harness = CreateHarness((attempt, context, cancellationToken) =>
                Task.FromResult(NodeExecutionResult.Failure("transient")));
            harness.Node.ExecutionPolicy.RetryPolicy.Enabled = true;
            harness.Node.ExecutionPolicy.RetryPolicy.MaxRetries = 5;
            harness.Node.ExecutionPolicy.RetryPolicy.RetryIntervalMs = 1000;
            PrepareRunner(harness);
            await harness.Runner.StartAsync().ConfigureAwait(false);
            using (var cancellation = new CancellationTokenSource())
            {
                var runTask = harness.Runner.TriggerAsync(
                    TestTriggerRequests.Manual("start", new FlowToken()),
                    cancellation.Token);
                await WaitForEventAsync(harness.Sink, FlowRuntimeEventType.NodeRetrying).ConfigureAwait(false);
                cancellation.Cancel();
                var result = await runTask.ConfigureAwait(false);

                AssertEx.Equal(FlowRunStatus.Cancelled, result.Status, "Cancellation should interrupt the retry interval.");
                AssertEx.Equal(1, harness.Factory.AttemptCount, "Cancellation during the interval should prevent the next attempt.");
                AssertEx.True(harness.Sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeCancelled),
                    "Cancellation should publish NodeCancelled.");
            }

            await harness.Runner.StopAsync().ConfigureAwait(false);
        }

        private static async Task<FlowRunResult> RunAsync(PolicyHarness harness)
        {
            PrepareRunner(harness);
            await harness.Runner.StartAsync().ConfigureAwait(false);
            var result = await harness.Runner.TriggerAsync(
                TestTriggerRequests.Manual("start", new FlowToken())).ConfigureAwait(false);
            await harness.Runner.StopAsync().ConfigureAwait(false);
            return result;
        }

        private static PolicyHarness CreateHarness(PolicyProbeAsync probe)
        {
            var sink = new InMemoryFlowEventSink();
            var factory = new PolicyProbeNodeFactory(probe);
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "policy-flow",
                FlowName = "Node execution policy",
                Version = "2.0.0"
            };
            var node = CreateNode("probe");
            flow.Nodes.Add(node);
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "start", TargetNodeId = node.Id });

            return new PolicyHarness
            {
                Factory = factory,
                Flow = flow,
                Node = node,
                Sink = sink
            };
        }

        private static void PrepareRunner(PolicyHarness harness)
        {
            var registry = new NodeRegistry();
            registry.Register(harness.Factory);
            harness.Runner = new FlowEngine(registry, harness.Sink).CreateRunner(harness.Flow);
        }

        private static NodeDefinition CreateNode(string id)
        {
            return new NodeDefinition
            {
                Id = id,
                Type = PolicyProbeNodeFactory.TypeName,
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

        private static void EnableManyRetries(NodeDefinition node)
        {
            node.ExecutionPolicy.RetryPolicy.Enabled = true;
            node.ExecutionPolicy.RetryPolicy.MaxRetries = 5;
            node.ExecutionPolicy.RetryPolicy.RetryIntervalMs = 0;
        }

        private static string ReadFinalFailureKind(InMemoryFlowEventSink sink)
        {
            var failed = sink.Events.Last(x => x.EventType == FlowRuntimeEventType.NodeFailed);
            return Convert.ToString(failed.Data[FlowRuntimeDataKeys.FailureKind]);
        }

        private static async Task WaitForEventAsync(InMemoryFlowEventSink sink, FlowRuntimeEventType eventType)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (sink.Events.Any(x => x.EventType == eventType))
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for event: " + eventType);
        }

        private delegate Task<NodeExecutionResult> PolicyProbeAsync(
            int attempt,
            FlowExecutionContext context,
            CancellationToken cancellationToken);

        private sealed class PolicyHarness
        {
            public PolicyProbeNodeFactory Factory { get; set; }

            public RuntimeFlowDefinition Flow { get; set; }

            public NodeDefinition Node { get; set; }

            public InMemoryFlowEventSink Sink { get; set; }

            public IFlowRunner Runner { get; set; }
        }

        private sealed class PolicyProbeNodeFactory : INodeFactory
        {
            public const string TypeName = "test.execution-policy";
            private readonly PolicyProbeAsync _probe;
            private int _attemptCount;

            public PolicyProbeNodeFactory(PolicyProbeAsync probe)
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
                        DisplayName = "执行策略测试节点",
                        Category = "测试",
                        Version = "1.0.0"
                    };
                }
            }

            public int AttemptCount
            {
                get { return _attemptCount; }
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return new PolicyProbeNode(this, _probe);
            }

            private sealed class PolicyProbeNode : IFlowNode
            {
                private readonly PolicyProbeNodeFactory _factory;
                private readonly PolicyProbeAsync _probe;

                public PolicyProbeNode(PolicyProbeNodeFactory factory, PolicyProbeAsync probe)
                {
                    _factory = factory;
                    _probe = probe;
                }

                public Task<NodeExecutionResult> ExecuteAsync(
                    FlowExecutionContext context,
                    CancellationToken cancellationToken)
                {
                    var attempt = Interlocked.Increment(ref _factory._attemptCount);
                    return _probe(attempt, context, cancellationToken);
                }
            }
        }
    }
}
