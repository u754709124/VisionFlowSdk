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
using Vision.Flow.Core.Runtime.Events;
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
    // 鎺у埗娴佽妭鐐规祴璇曡鐩栧垎鏀拰姹囧悎璇箟锛屼笉寮曞叆璁惧閫傞厤鍣ㄣ€?
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

        public static async Task AndJoinAcceptsStrongDuplicatePolicy()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateAndJoinFlow(FlowDuplicatePolicy.Error, includeErrorHandler: true), executionLog, sink);
            var token = new FlowToken { TokenId = "duplicate-token-enum", PositionId = "P01" };

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "ErrorHandler" }, executionLog, "DuplicatePolicy enum should route duplicate inputs through Error.");
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

        public static async Task ConditionAcceptsStrongOperator()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateConditionFlow(ConditionOperator.Equal), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "condition-token-enum", PositionId = "P01" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "TrueNode" }, executionLog, "Condition operator enum should route matching tokens.");
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
            return CreateAndJoinFlow((object)duplicatePolicy, includeErrorHandler);
        }

        private static RuntimeFlowDefinition CreateAndJoinFlow(FlowDuplicatePolicy duplicatePolicy, bool includeErrorHandler)
        {
            return CreateAndJoinFlow((object)duplicatePolicy, includeErrorHandler);
        }

        private static RuntimeFlowDefinition CreateAndJoinFlow(object duplicatePolicy, bool includeErrorHandler)
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
            return CreateConditionFlow("Equal");
        }

        private static RuntimeFlowDefinition CreateConditionFlow(ConditionOperator operatorName)
        {
            return CreateConditionFlow((object)operatorName);
        }

        private static RuntimeFlowDefinition CreateConditionFlow(object operatorName)
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
                    { "Operator", operatorName },
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
