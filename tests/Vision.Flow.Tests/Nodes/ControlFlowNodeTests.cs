using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // 控制流节点测试覆盖分支和汇合语义，不引入设备适配器。
    internal static class ControlFlowNodeTests
    {
        public static async Task AndJoinTwoInputsSameJoinKey()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateAndJoinFlow("Ignore", includeErrorHandler: false), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-1", PositionId = "P01" }).ConfigureAwait(false);
            AssertEx.Equal(0, executionLog.Count, "First input should wait for another token with the same JoinKey.");

            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-2", PositionId = "P01" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "Done" }, executionLog, "Second input with the same JoinKey should complete the join.");
            AssertEx.Equal(true, FindLastOutput(sink, "join1", "Result"), "Completed join should output Result=true.");
            AssertEx.Equal(true, FindLastOutput(sink, "join1", "IsMatched"), "Completed join should output IsMatched=true.");
        }

        public static async Task AndJoinDifferentKeysDoNotMix()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateAndJoinFlow("Ignore", includeErrorHandler: false), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-a", PositionId = "P01" }).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-b", PositionId = "P02" }).ConfigureAwait(false);

            AssertEx.Equal(0, executionLog.Count, "Different JoinKeys should remain in separate waiting buckets.");
            AssertEx.Equal(2, CountOutputValues(sink, "join1", "IsMatched", false), "Both different keys should report waiting outputs.");
        }

        public static async Task AndJoinDuplicatePolicyError()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateAndJoinFlow("Error", includeErrorHandler: true), executionLog, sink);
            var token = new FlowToken { TokenId = "duplicate-token", PositionId = "P01" };

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "ErrorHandler" }, executionLog, "DuplicatePolicy=Error should route duplicate inputs through Error.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeFailed && string.Equals(x.NodeId, "join1", StringComparison.OrdinalIgnoreCase)),
                "Duplicate join input should publish NodeFailed.");
        }

        public static async Task ConditionTrueFalseRoutes()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateConditionFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "condition-token-true", PositionId = "P01" }).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "condition-token-false", PositionId = "P02" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "TrueNode", "FalseNode" }, executionLog, "ConditionNode should route matching and non-matching tokens.");
            AssertEx.Equal(1, CountOutputValues(sink, "condition1", "IsMatched", true), "True branch should produce IsMatched=true once.");
            AssertEx.Equal(1, CountOutputValues(sink, "condition1", "IsMatched", false), "False branch should produce IsMatched=false once.");
        }

        private static IFlowRunner CreateRunner(RuntimeFlowDefinition flow, IList<string> executionLog, InMemoryFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new RecordingNodeFactory(executionLog));
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateAndJoinFlow(string duplicatePolicy, bool includeErrorHandler)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "and-join",
                FlowName = "AND Join",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = AndJoinNodeFactory.TypeName,
                Name = "Join",
                Version = "1.0.0",
                Settings =
                {
                    { "JoinKeyBinding", "{{ token.PositionId }}" },
                    { "ExpectedInputCount", 2 },
                    { "TimeoutMs", 0 },
                    { "DuplicatePolicy", duplicatePolicy }
                }
            });
            flow.Nodes.Add(CreateRecordNode("Done"));
            flow.Edges.Add(CreateEdge("join1", "Next", "Done"));

            if (includeErrorHandler)
            {
                flow.Nodes.Add(CreateRecordNode("ErrorHandler"));
                flow.Edges.Add(CreateEdge("join1", "Error", "ErrorHandler"));
            }

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateConditionFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "condition",
                FlowName = "Condition",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "condition1",
                Type = ConditionNodeFactory.TypeName,
                Name = "Condition",
                Version = "1.0.0",
                Settings =
                {
                    { "LeftBinding", "{{ token.PositionId }}" },
                    { "Operator", "Equal" },
                    { "RightValue", "P01" }
                }
            });
            flow.Nodes.Add(CreateRecordNode("TrueNode"));
            flow.Nodes.Add(CreateRecordNode("FalseNode"));
            flow.Edges.Add(CreateEdge("condition1", "True", "TrueNode"));
            flow.Edges.Add(CreateEdge("condition1", "False", "FalseNode"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "condition1" });
            return flow;
        }

        private static NodeDefinition CreateRecordNode(string id)
        {
            return new NodeDefinition
            {
                Id = id,
                Type = RecordingNodeFactory.TypeName,
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
                ToPort = "In"
            };
        }

        private static object FindLastOutput(InMemoryFlowEventSink sink, string nodeId, string outputName)
        {
            var variableName = nodeId + "." + outputName;
            var runtimeEvent = sink.Events.LastOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), variableName, StringComparison.OrdinalIgnoreCase));
            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data[FlowRuntimeDataKeys.Value];
        }

        private static int CountOutputValues(InMemoryFlowEventSink sink, string nodeId, string outputName, object expectedValue)
        {
            var variableName = nodeId + "." + outputName;
            return sink.Events.Count(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), variableName, StringComparison.OrdinalIgnoreCase) &&
                object.Equals(x.Data[FlowRuntimeDataKeys.Value], expectedValue));
        }
    }
}
