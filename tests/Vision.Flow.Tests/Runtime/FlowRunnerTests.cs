using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Nodes;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // �������������Ը��ǵ��ȡ�·�ɡ�ȡ���������¼���Ϊ��
    internal static class FlowRunnerTests
    {
        public static async Task LinearOrderAndVariables()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateLinearFlow(includeOutputs: true), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-linear" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "C" }, executionLog, "Nodes should execute in A -> B -> C order.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.OutputProduced && Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]) == "A.Value"), "A.Value output should be written.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.OutputProduced && Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]) == "B.Value"), "B.Value output should be written.");
        }

        public static async Task FanOutExecutesAllOutgoingEdges()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateFanOutFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-fanout" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "C" }, executionLog, "All edges from A.Next should execute in definition order.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeCompleted && string.Equals(x.NodeId, "B", StringComparison.OrdinalIgnoreCase)),
                "Fan-out branch B should complete.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeCompleted && string.Equals(x.NodeId, "C", StringComparison.OrdinalIgnoreCase)),
                "Fan-out branch C should complete.");
        }

        public static async Task ParallelFanOutExecutesBranchesInParallel()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(
                CreateDelayedFanOutFlow(160),
                executionLog,
                sink,
                new FlowExecutionOptions
                {
                    FanOutMode = FlowFanOutMode.Parallel,
                    MaxDegreeOfParallelism = 2
                });

            await runner.StartAsync().ConfigureAwait(false);
            var startUtc = DateTime.UtcNow;
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-parallel-fanout" }).ConfigureAwait(false);
            var elapsedMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;

            AssertEx.True(executionLog.Contains("A"), "Parallel fan-out should execute the source node.");
            AssertEx.True(executionLog.Contains("B"), "Parallel fan-out should execute branch B.");
            AssertEx.True(executionLog.Contains("C"), "Parallel fan-out should execute branch C.");
            AssertEx.True(elapsedMs < 280, "Parallel fan-out should complete faster than two sequential delayed branches. ElapsedMs=" + elapsedMs.ToString(CultureInfo.InvariantCulture));
        }

        public static async Task BranchedFanOutGraphExecutesAllBranches()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateBranchedFanOutFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-branched-fanout" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "D", "C", "E" }, executionLog, "Both fan-out branches should execute their downstream chains.");
        }

        public static async Task ReconvergingBranchesCanReachSameNode()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateReconvergingFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-reconverge" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "D", "C", "D" }, executionLog, "Path-level cycle detection should not block a valid reconverging node.");
        }

        public static async Task NodeFailedAndErrorRoute()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateFailureFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-failure" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "ErrorHandler" }, executionLog, "Failure should follow the Error route.");
            var failedEvent = sink.Events.FirstOrDefault(x => x.EventType == FlowRuntimeEventType.NodeFailed);
            AssertEx.NotNull(failedEvent, "NodeFailed event should be published.");
            AssertEx.Equal("A", failedEvent.NodeId, "NodeFailed should identify the failing node.");
            AssertEx.Equal("Error", failedEvent.OutputPort, "NodeFailed should use the Error output port.");
        }

        public static async Task NodeTimeoutAndTimeoutRoute()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateTimeoutFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-timeout-route" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "TimeoutHandler" }, executionLog, "Timeout should follow the Timeout route.");
            var timeoutEvent = sink.Events.FirstOrDefault(x => x.EventType == FlowRuntimeEventType.NodeTimeout);
            AssertEx.NotNull(timeoutEvent, "NodeTimeout event should be published.");
            AssertEx.Equal("A", timeoutEvent.NodeId, "NodeTimeout should identify the timed out node.");
            AssertEx.Equal("Timeout", timeoutEvent.OutputPort, "NodeTimeout should use the configured Timeout output port.");
        }

        public static async Task StopAsyncCancelsRunningFlow()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateLongRunningFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            var triggerTask = runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-stop" });
            await WaitForEventAsync(sink, FlowRuntimeEventType.NodeStarted, "A").ConfigureAwait(false);
            await runner.StopAsync().ConfigureAwait(false);

            await AssertEx.ThrowsAsync<OperationCanceledException>(
                async delegate
                {
                    await triggerTask.ConfigureAwait(false);
                }).ConfigureAwait(false);

            AssertEx.False(runner.IsRunning, "StopAsync should mark the runner as stopped.");
        }

        public static async Task ContinuationDispatcherRoutesOutputPort()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateContinuationFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-continuation" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B" }, executionLog, "Continuation should route A.Frame to B.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.OutputProduced && Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]) == "A.Value"),
                "Continuation outputs should be written through the source node namespace.");
        }

        public static async Task CycleRouteThrows()
        {
            var runner = CreateRunner(CreateCycleFlow(), new List<string>(), new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);

            var exception = await AssertEx.ThrowsAsync<InvalidOperationException>(
                () => runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-cycle" })).ConfigureAwait(false);

            AssertEx.True(exception.Message.IndexOf("Cycle detected", StringComparison.OrdinalIgnoreCase) >= 0, "Cycle detection should report a clear error.");
        }

        public static async Task MissingEntryThrows()
        {
            var runner = CreateRunner(CreateLinearFlow(includeOutputs: false), new List<string>(), new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);

            var exception = await AssertEx.ThrowsAsync<ArgumentException>(
                () => runner.TriggerAsync("MissingEntry", new FlowToken())).ConfigureAwait(false);

            AssertEx.True(exception.Message.IndexOf("MissingEntry", StringComparison.OrdinalIgnoreCase) >= 0, "Missing entry exception should include the entry name.");
        }

        public static async Task RuntimeEventOrder()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateSingleNodeFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-events" }).ConfigureAwait(false);

            var eventTypes = sink.Events.Select(x => x.EventType).ToList();
            AssertEx.SequenceEqual(
                new[]
                {
                    FlowRuntimeEventType.FlowStarted,
                    FlowRuntimeEventType.TokenCreated,
                    FlowRuntimeEventType.NodeStarted,
                    FlowRuntimeEventType.NodeCompleted
                },
                eventTypes,
                "Runtime events should be published in execution order.");

            var completed = sink.Events.FirstOrDefault(x => x.EventType == FlowRuntimeEventType.NodeCompleted);
            AssertEx.NotNull(completed, "NodeCompleted event should be published.");
            AssertEx.False(string.IsNullOrWhiteSpace(completed.FlowRunId), "Runtime events should include FlowRunId.");
            AssertEx.True(completed.ElapsedMs >= 0, "Runtime events should include elapsed time.");
        }

        private static IFlowRunner CreateRunner(RuntimeFlowDefinition flow, IList<string> executionLog, InMemoryFlowEventSink sink)
        {
            return CreateRunner(flow, executionLog, sink, null);
        }

        private static IFlowRunner CreateRunner(RuntimeFlowDefinition flow, IList<string> executionLog, InMemoryFlowEventSink sink, FlowExecutionOptions options)
        {
            var registry = new NodeRegistry();
            registry.Register(new RecordingNodeFactory(executionLog));
            return new FlowEngine(registry, sink, null, null, null, options).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateLinearFlow(bool includeOutputs)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "linear",
                FlowName = "Linear",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", includeOutputs ? "Value" : null, null));
            flow.Nodes.Add(CreateNode("B", includeOutputs ? "Value" : null, includeOutputs ? "A.Value" : null));
            flow.Nodes.Add(CreateNode("C", null, includeOutputs ? "B.Value" : null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("B", "Next", "C"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateFanOutFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "fanout",
                FlowName = "Fan Out",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Nodes.Add(CreateNode("C", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("A", "Next", "C"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateDelayedFanOutFlow(int delayMs)
        {
            var flow = CreateFanOutFlow();
            flow.FlowId = "parallel-fanout";
            flow.FlowName = "Parallel Fan Out";
            flow.Nodes[1].Settings["DelayMs"] = delayMs;
            flow.Nodes[2].Settings["DelayMs"] = delayMs;
            return flow;
        }

        private static RuntimeFlowDefinition CreateBranchedFanOutFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "branched-fanout",
                FlowName = "Branched Fan Out",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Nodes.Add(CreateNode("C", null, null));
            flow.Nodes.Add(CreateNode("D", null, null));
            flow.Nodes.Add(CreateNode("E", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("A", "Next", "C"));
            flow.Edges.Add(CreateEdge("B", "Next", "D"));
            flow.Edges.Add(CreateEdge("C", "Next", "E"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateReconvergingFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "reconverging",
                FlowName = "Reconverging",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Nodes.Add(CreateNode("C", null, null));
            flow.Nodes.Add(CreateNode("D", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("A", "Next", "C"));
            flow.Edges.Add(CreateEdge("B", "Next", "D"));
            flow.Edges.Add(CreateEdge("C", "Next", "D"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateFailureFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "failure",
                FlowName = "Failure",
                Version = "1.0.0"
            };

            var failing = CreateNode("A", null, null);
            failing.Settings["Mode"] = "Fail";
            flow.Nodes.Add(failing);
            flow.Nodes.Add(CreateNode("ErrorHandler", null, null));
            flow.Edges.Add(CreateEdge("A", "Error", "ErrorHandler"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateLongRunningFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stop-flow",
                FlowName = "Stop Flow",
                Version = "1.0.0"
            };

            var node = CreateNode("A", null, null);
            node.Settings["DelayMs"] = 1000;
            flow.Nodes.Add(node);
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateContinuationFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "continuation",
                FlowName = "Continuation",
                Version = "1.0.0"
            };

            var source = CreateNode("A", null, null);
            source.Settings["Mode"] = "ContinueFrame";
            source.Settings["ContinuationOutputName"] = "Value";
            source.Settings["ContinuationOutputValue"] = "continued";
            flow.Nodes.Add(source);
            flow.Nodes.Add(CreateNode("B", null, "A.Value"));
            flow.Edges.Add(CreateEdge("A", "Frame", "B"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateTimeoutFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "timeout",
                FlowName = "Timeout",
                Version = "1.0.0"
            };

            var timedOut = CreateNode("A", null, null);
            timedOut.Settings["Mode"] = "Timeout";
            timedOut.Settings["TimeoutOutputPort"] = "Timeout";
            flow.Nodes.Add(timedOut);
            flow.Nodes.Add(CreateNode("TimeoutHandler", null, null));
            flow.Edges.Add(CreateEdge("A", "Timeout", "TimeoutHandler"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateCycleFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "cycle",
                FlowName = "Cycle",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("B", "Next", "A"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateSingleNodeFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "single",
                FlowName = "Single",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static NodeDefinition CreateNode(string id, string outputName, string requiredVariable)
        {
            var node = new NodeDefinition
            {
                Id = id,
                Type = RecordingNodeFactory.TypeName,
                Name = id,
                Version = "1.0.0"
            };

            if (!string.IsNullOrWhiteSpace(outputName))
            {
                node.Settings["OutputName"] = outputName;
                node.Settings["OutputValue"] = id + "-output";
            }

            if (!string.IsNullOrWhiteSpace(requiredVariable))
            {
                node.Settings["RequiredVariable"] = requiredVariable;
            }

            return node;
        }

        private static EdgeDefinition CreateEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            return new EdgeDefinition
            {
                FromNodeId = fromNodeId,
                FromPort = fromPort,
                ToNodeId = toNodeId,
                ToPort = "In"
            };
        }

        private static async Task WaitForEventAsync(InMemoryFlowEventSink sink, FlowRuntimeEventType eventType, string nodeId)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (sink.Events.Any(x =>
                    x.EventType == eventType &&
                    string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for event " + eventType + " on node " + nodeId + ".");
        }
    }
}
