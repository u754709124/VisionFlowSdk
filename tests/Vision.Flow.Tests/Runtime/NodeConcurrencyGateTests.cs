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

namespace Vision.Flow.Tests
{
    internal static class NodeConcurrencyGateTests
    {
        public static async Task DefaultLimitSerializesSameNodeAcrossRuns()
        {
            var factory = new GateProbeNodeFactory();
            var flow = CreateSingleNodeFlow(1);
            var runner = CreateRunner(flow, factory, new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);
            try
            {
                var first = TriggerAsync(runner, "start", "first");
                await factory.WaitForNodeStartsAsync("probe", 1).ConfigureAwait(false);
                var second = TriggerAsync(runner, "start", "second");
                await Task.Delay(80).ConfigureAwait(false);

                AssertEx.Equal(1, factory.GetNodeStartCount("probe"),
                    "The default node limit should keep the second FlowRun waiting.");
                factory.ReleaseAll();
                await Task.WhenAll(first, second).ConfigureAwait(false);

                AssertEx.Equal(2, factory.GetNodeStartCount("probe"), "Both FlowRuns should eventually execute the node.");
                AssertEx.Equal(1, factory.GetNodeMaximumActive("probe"), "The same NodeId should execute serially by default.");
            }
            finally
            {
                factory.ReleaseAll();
                await runner.StopAsync().ConfigureAwait(false);
            }
        }

        public static async Task ConfiguredLimitAllowsTwoSameNodeRuns()
        {
            var factory = new GateProbeNodeFactory();
            var flow = CreateSingleNodeFlow(2);
            var runner = CreateRunner(flow, factory, new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);
            try
            {
                var first = TriggerAsync(runner, "start", "first");
                var second = TriggerAsync(runner, "start", "second");
                await factory.WaitForNodeStartsAsync("probe", 2).ConfigureAwait(false);

                AssertEx.Equal(2, factory.GetNodeMaximumActive("probe"),
                    "MaxConcurrentExecutions=2 should allow two FlowRuns inside the same node.");
                factory.ReleaseAll();
                await Task.WhenAll(first, second).ConfigureAwait(false);
            }
            finally
            {
                factory.ReleaseAll();
                await runner.StopAsync().ConfigureAwait(false);
            }
        }

        public static async Task DifferentNodesDoNotShareGate()
        {
            var factory = new GateProbeNodeFactory();
            var flow = CreateTwoNodeFlow();
            var runner = CreateRunner(flow, factory, new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);
            try
            {
                var first = TriggerAsync(runner, "start-a", "first");
                await factory.WaitForNodeStartsAsync("probe-a", 1).ConfigureAwait(false);
                var second = TriggerAsync(runner, "start-b", "second");
                await factory.WaitForNodeStartsAsync("probe-b", 1).ConfigureAwait(false);

                AssertEx.Equal(2, factory.MaximumActiveAcrossNodes,
                    "Different NodeIds should be able to execute at the same time.");
                factory.ReleaseAll();
                await Task.WhenAll(first, second).ConfigureAwait(false);
            }
            finally
            {
                factory.ReleaseAll();
                await runner.StopAsync().ConfigureAwait(false);
            }
        }

        public static async Task WaitingForGateCanBeCancelled()
        {
            var factory = new GateProbeNodeFactory();
            var flow = CreateSingleNodeFlow(1);
            var runner = CreateRunner(flow, factory, new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);
            try
            {
                var first = TriggerAsync(runner, "start", "first");
                await factory.WaitForNodeStartsAsync("probe", 1).ConfigureAwait(false);
                using (var cancellation = new CancellationTokenSource())
                {
                    var waiting = TriggerAsync(runner, "start", "waiting", cancellation.Token);
                    await Task.Delay(80).ConfigureAwait(false);
                    cancellation.Cancel();
                    var waitingResult = await waiting.ConfigureAwait(false);

                    AssertEx.Equal(FlowRunStatus.Cancelled, waitingResult.Status,
                        "A FlowRun cancelled while waiting for the node gate should be cancelled.");
                    AssertEx.Equal(1, factory.GetNodeStartCount("probe"),
                        "A cancelled waiter must not execute the node.");
                }

                factory.ReleaseAll();
                var firstResult = await first.ConfigureAwait(false);
                AssertEx.Equal(FlowRunStatus.Succeeded, firstResult.Status, "The gate owner should finish normally.");
            }
            finally
            {
                factory.ReleaseAll();
                await runner.StopAsync().ConfigureAwait(false);
            }
        }

        public static async Task RetryIntervalKeepsGateLease()
        {
            var sink = new InMemoryFlowEventSink();
            var factory = new GateProbeNodeFactory();
            factory.Behavior = (context, tokenAttempt, cancellationToken) => Task.FromResult(
                string.Equals(context.Token.TokenId, "first", StringComparison.Ordinal) && tokenAttempt == 1
                    ? NodeExecutionResult.Failure("transient")
                    : NodeExecutionResult.Success());
            var flow = CreateSingleNodeFlow(1);
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.Enabled = true;
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.MaxRetries = 1;
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.RetryIntervalMs = 250;
            var runner = CreateRunner(flow, factory, sink);
            await runner.StartAsync().ConfigureAwait(false);
            try
            {
                var first = TriggerAsync(runner, "start", "first");
                await WaitForEventAsync(sink, FlowRuntimeEventType.NodeRetrying).ConfigureAwait(false);
                var second = TriggerAsync(runner, "start", "second");
                await Task.Delay(80).ConfigureAwait(false);

                AssertEx.Equal(0, factory.GetTokenStartCount("second"),
                    "The node gate should remain held during the retry interval.");
                var results = await Task.WhenAll(first, second).ConfigureAwait(false);
                AssertEx.True(results.All(x => x.Status == FlowRunStatus.Succeeded),
                    "The retry owner and the waiting FlowRun should both finish successfully.");
                AssertEx.Equal(1, factory.GetNodeMaximumActive("probe"),
                    "Retries and other FlowRuns should share one node-level lease.");
            }
            finally
            {
                factory.ReleaseAll();
                await runner.StopAsync().ConfigureAwait(false);
            }
        }

        public static async Task TimedOutAttemptDrainsBeforeRetry()
        {
            var releaseFirstAttempt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var factory = new GateProbeNodeFactory();
            factory.Behavior = async delegate(FlowExecutionContext context, int tokenAttempt, CancellationToken cancellationToken)
            {
                if (tokenAttempt == 1)
                {
                    await releaseFirstAttempt.Task.ConfigureAwait(false);
                }

                return NodeExecutionResult.Success();
            };

            var flow = CreateSingleNodeFlow(1);
            flow.Nodes[0].ExecutionPolicy.TimeoutMs = 40;
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.Enabled = true;
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.MaxRetries = 1;
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.RetryIntervalMs = 0;
            var runner = CreateRunner(flow, factory, new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);
            try
            {
                var run = TriggerAsync(runner, "start", "timeout-drain");
                await Task.Delay(100).ConfigureAwait(false);

                AssertEx.Equal(1, factory.GetTokenStartCount("timeout-drain"),
                    "A retry must wait until the timed-out attempt has stopped.");
                releaseFirstAttempt.TrySetResult(true);
                var result = await run.ConfigureAwait(false);

                AssertEx.Equal(FlowRunStatus.Succeeded, result.Status,
                    "The drained timeout attempt should be followed by the configured retry.");
                AssertEx.Equal(2, factory.GetTokenStartCount("timeout-drain"),
                    "Exactly one retry should start after the previous attempt drains.");
                AssertEx.Equal(1, factory.GetNodeMaximumActive("probe"),
                    "Timed-out attempts and retries must not overlap inside one node gate.");
            }
            finally
            {
                releaseFirstAttempt.TrySetResult(true);
                await runner.StopAsync().ConfigureAwait(false);
            }
        }

        private static RuntimeFlowDefinition CreateSingleNodeFlow(int maxConcurrentExecutions)
        {
            var flow = new RuntimeFlowDefinition { FlowId = "node-gate", FlowName = "Node gate", Version = "2.0.0" };
            flow.Nodes.Add(CreateNode("probe", maxConcurrentExecutions));
            flow.Entries.Add(CreateEntry("start", "probe"));
            return flow;
        }

        private static RuntimeFlowDefinition CreateTwoNodeFlow()
        {
            var flow = new RuntimeFlowDefinition { FlowId = "node-gate-isolation", FlowName = "Node gate isolation", Version = "2.0.0" };
            flow.Nodes.Add(CreateNode("probe-a", 1));
            flow.Nodes.Add(CreateNode("probe-b", 1));
            flow.Entries.Add(CreateEntry("start-a", "probe-a"));
            flow.Entries.Add(CreateEntry("start-b", "probe-b"));
            return flow;
        }

        private static NodeDefinition CreateNode(string id, int maxConcurrentExecutions)
        {
            var node = new NodeDefinition
            {
                Id = id,
                Type = GateProbeNodeFactory.TypeName,
                Name = id,
                Version = "1.0.0"
            };
            node.ExecutionPolicy.MaxConcurrentExecutions = maxConcurrentExecutions;
            return node;
        }

        private static FlowEntryDefinition CreateEntry(string name, string targetNodeId)
        {
            return new FlowEntryDefinition
            {
                EntryName = name,
                TargetNodeId = targetNodeId,
                ExecutionPolicy = new TriggerExecutionPolicy
                {
                    MaxConcurrentRuns = 4,
                    QueueCapacity = 4,
                    QueueFullBehavior = TriggerQueueFullBehavior.Reject
                }
            };
        }

        private static IFlowRunner CreateRunner(
            RuntimeFlowDefinition flow,
            GateProbeNodeFactory factory,
            IFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            registry.Register(factory);
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static Task<FlowRunResult> TriggerAsync(
            IFlowRunner runner,
            string entryName,
            string tokenId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return runner.TriggerAsync(
                TestTriggerRequests.Manual(entryName, new FlowToken { TokenId = tokenId }),
                cancellationToken);
        }

        private static async Task WaitForEventAsync(InMemoryFlowEventSink sink, FlowRuntimeEventType eventType)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (sink.Events.Any(x => x.EventType == eventType))
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for runtime event: " + eventType);
        }

        private delegate Task<NodeExecutionResult> ProbeBehavior(
            FlowExecutionContext context,
            int tokenAttempt,
            CancellationToken cancellationToken);

        private sealed class GateProbeNodeFactory : INodeFactory
        {
            public const string TypeName = "test.node-concurrency-gate";
            private readonly object _gate = new object();
            private readonly Dictionary<string, int> _activeByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _maximumByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _startsByNode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _startsByToken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private readonly TaskCompletionSource<bool> _release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _activeAcrossNodes;

            public GateProbeNodeFactory()
            {
                Behavior = WaitUntilReleasedAsync;
            }

            public string NodeType { get { return TypeName; } }

            public NodeDescriptor Descriptor
            {
                get
                {
                    return new NodeDescriptor
                    {
                        NodeType = TypeName,
                        DisplayName = "节点并发闸门测试",
                        Category = "测试",
                        Version = "1.0.0"
                    };
                }
            }

            public ProbeBehavior Behavior { get; set; }

            public int MaximumActiveAcrossNodes { get; private set; }

            public IFlowNode Create(NodeDefinition definition)
            {
                return new GateProbeNode(this, definition.Id);
            }

            public void ReleaseAll()
            {
                _release.TrySetResult(true);
            }

            public int GetNodeStartCount(string nodeId)
            {
                lock (_gate)
                {
                    return ReadCount(_startsByNode, nodeId);
                }
            }

            public int GetTokenStartCount(string tokenId)
            {
                lock (_gate)
                {
                    return ReadCount(_startsByToken, tokenId);
                }
            }

            public int GetNodeMaximumActive(string nodeId)
            {
                lock (_gate)
                {
                    return ReadCount(_maximumByNode, nodeId);
                }
            }

            public async Task WaitForNodeStartsAsync(string nodeId, int expected)
            {
                for (var attempt = 0; attempt < 200; attempt++)
                {
                    if (GetNodeStartCount(nodeId) >= expected)
                    {
                        return;
                    }

                    await Task.Delay(10).ConfigureAwait(false);
                }

                throw new InvalidOperationException("Timed out waiting for node starts: " + nodeId);
            }

            private async Task<NodeExecutionResult> WaitUntilReleasedAsync(
                FlowExecutionContext context,
                int tokenAttempt,
                CancellationToken cancellationToken)
            {
                var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);
                var completed = await Task.WhenAny(_release.Task, cancellationTask).ConfigureAwait(false);
                if (completed != _release.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return NodeExecutionResult.Success();
            }

            private int Enter(string nodeId, string tokenId)
            {
                lock (_gate)
                {
                    var active = ReadCount(_activeByNode, nodeId) + 1;
                    _activeByNode[nodeId] = active;
                    _maximumByNode[nodeId] = Math.Max(ReadCount(_maximumByNode, nodeId), active);
                    _startsByNode[nodeId] = ReadCount(_startsByNode, nodeId) + 1;
                    var tokenAttempt = ReadCount(_startsByToken, tokenId) + 1;
                    _startsByToken[tokenId] = tokenAttempt;
                    _activeAcrossNodes++;
                    MaximumActiveAcrossNodes = Math.Max(MaximumActiveAcrossNodes, _activeAcrossNodes);
                    return tokenAttempt;
                }
            }

            private void Exit(string nodeId)
            {
                lock (_gate)
                {
                    _activeByNode[nodeId] = ReadCount(_activeByNode, nodeId) - 1;
                    _activeAcrossNodes--;
                }
            }

            private static int ReadCount(IDictionary<string, int> values, string key)
            {
                int value;
                return values.TryGetValue(key, out value) ? value : 0;
            }

            private sealed class GateProbeNode : IFlowNode
            {
                private readonly GateProbeNodeFactory _factory;
                private readonly string _nodeId;

                public GateProbeNode(GateProbeNodeFactory factory, string nodeId)
                {
                    _factory = factory;
                    _nodeId = nodeId;
                }

                public async Task<NodeExecutionResult> ExecuteAsync(
                    FlowExecutionContext context,
                    CancellationToken cancellationToken)
                {
                    var tokenAttempt = _factory.Enter(_nodeId, context.Token.TokenId);
                    try
                    {
                        return await _factory.Behavior(context, tokenAttempt, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _factory.Exit(_nodeId);
                    }
                }
            }
        }
    }
}
