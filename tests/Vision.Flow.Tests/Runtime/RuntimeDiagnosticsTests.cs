using System;
using System.Collections;
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
    internal static class RuntimeDiagnosticsTests
    {
        public static async Task GateCanEnableCaptureWithoutRecreatingRunner()
        {
            var gate = new MutableDiagnosticsGate();
            DiagnosticsHarness harness = CreateHarness(gate, (attempt, context) =>
            {
                context.GetSettingValue("Used");
                context.GetSettingValue("Binary");
                return NodeExecutionResult.Success();
            });

            await harness.Runner.StartAsync().ConfigureAwait(false);
            await harness.Runner.TriggerAsync(TestTriggerRequests.Manual("start", new FlowToken())).ConfigureAwait(false);
            AssertEx.False(harness.Sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeInputsCaptured),
                "关闭诊断时不应发布输入事件。");

            gate.Enabled = true;
            await harness.Runner.TriggerAsync(TestTriggerRequests.Manual("start", new FlowToken())).ConfigureAwait(false);
            await harness.Runner.StopAsync().ConfigureAwait(false);

            FlowRuntimeEvent captured = harness.Sink.Events.Single(x =>
                x.EventType == FlowRuntimeEventType.NodeInputsCaptured);
            IList<IDictionary<string, object>> inputs = ReadInputs(captured);
            AssertEx.SequenceEqual(new[] { "Used", "Binary" },
                inputs.Select(x => Convert.ToString(x[FlowRuntimeDataKeys.SettingName])),
                "只应按实际读取顺序捕获输入，未读取设置不得进入事件。");
            AssertEx.Equal("raw-value", Convert.ToString(inputs[0][FlowRuntimeDataKeys.Value]),
                "字符串应保留原始内容而不做脱敏。");
            AssertEx.True(inputs[1][FlowRuntimeDataKeys.Value] is FlowRuntimeValueSummary,
                "二进制输入必须由事件安全器转换为轻量摘要。");
        }

        public static async Task RetryAttemptsPublishIndependentInputSnapshots()
        {
            var gate = new MutableDiagnosticsGate { Enabled = true };
            DiagnosticsHarness harness = CreateHarness(gate, (attempt, context) =>
            {
                context.GetSettingValue("Used");
                return attempt == 1
                    ? NodeExecutionResult.Failure("transient")
                    : NodeExecutionResult.Success();
            });
            harness.Node.ExecutionPolicy.RetryPolicy.Enabled = true;
            harness.Node.ExecutionPolicy.RetryPolicy.MaxRetries = 1;

            await RunAsync(harness).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { 1, 2 },
                harness.Sink.Events
                    .Where(x => x.EventType == FlowRuntimeEventType.NodeInputsCaptured)
                    .Select(x => Convert.ToInt32(x.Data[FlowRuntimeDataKeys.Attempt])),
                "每个重试 Attempt 应发布独立输入快照。");
        }

        public static async Task ResolutionFailureIsCapturedBeforeNodeFailure()
        {
            var gate = new MutableDiagnosticsGate { Enabled = true };
            DiagnosticsHarness harness = CreateHarness(gate, (attempt, context) =>
            {
                context.GetSettingValue("MissingOutput");
                return NodeExecutionResult.Success();
            });
            harness.Node.Settings["MissingOutput"] = NodeSettingValue.ForVariable(
                VariableSelector.ForNodeOutput("missing", "Value"));

            await RunAsync(harness).ConfigureAwait(false);

            int inputIndex = FindEventIndex(harness.Sink, FlowRuntimeEventType.NodeInputsCaptured);
            int failureIndex = FindEventIndex(harness.Sink, FlowRuntimeEventType.NodeFailed);
            AssertEx.True(inputIndex >= 0 && failureIndex > inputIndex,
                "解析失败输入必须在节点终态失败事件之前发布。");
            IDictionary<string, object> input = ReadInputs(harness.Sink.Events[inputIndex]).Single();
            AssertEx.True(!string.IsNullOrWhiteSpace(
                    Convert.ToString(input[FlowRuntimeDataKeys.ResolutionError])),
                "失败输入应包含稳定的解析错误说明。");
        }

        private static DiagnosticsHarness CreateHarness(
            MutableDiagnosticsGate gate,
            Func<int, FlowExecutionContext, NodeExecutionResult> behavior)
        {
            var sink = new InMemoryFlowEventSink();
            var factory = new DiagnosticsNodeFactory(behavior);
            var node = new NodeDefinition
            {
                Id = "probe",
                Name = "probe",
                Type = DiagnosticsNodeFactory.TypeName,
                Version = "1.0.0"
            };
            node.Settings["Used"] = NodeSettingValue.ForConstant("raw-value");
            node.Settings["Unused"] = NodeSettingValue.ForConstant("must-not-appear");
            node.Settings["Binary"] = NodeSettingValue.ForConstant(new byte[] { 1, 2, 3 });
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "diagnostics-flow",
                FlowName = "Diagnostics",
                Version = "2.0.0"
            };
            flow.Nodes.Add(node);
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "start",
                TargetNodeId = node.Id
            });
            var registry = new NodeRegistry();
            registry.Register(factory);
            var runner = new FlowRunner(
                flow,
                registry,
                sink,
                null,
                new FlowExecutionOptions { DiagnosticsGate = gate });
            return new DiagnosticsHarness
            {
                Runner = runner,
                Sink = sink,
                Node = node
            };
        }

        private static async Task RunAsync(DiagnosticsHarness harness)
        {
            await harness.Runner.StartAsync().ConfigureAwait(false);
            await harness.Runner.TriggerAsync(
                TestTriggerRequests.Manual("start", new FlowToken())).ConfigureAwait(false);
            await harness.Runner.StopAsync().ConfigureAwait(false);
        }

        private static int FindEventIndex(InMemoryFlowEventSink sink, FlowRuntimeEventType eventType)
        {
            return sink.Events
                .Select((runtimeEvent, index) => new { runtimeEvent, index })
                .Where(x => x.runtimeEvent.EventType == eventType)
                .Select(x => x.index)
                .DefaultIfEmpty(-1)
                .First();
        }

        private static IList<IDictionary<string, object>> ReadInputs(FlowRuntimeEvent runtimeEvent)
        {
            var result = new List<IDictionary<string, object>>();
            var values = runtimeEvent.Data[FlowRuntimeDataKeys.Inputs] as IEnumerable;
            foreach (object value in values)
                result.Add((IDictionary<string, object>)value);
            return result;
        }

        private sealed class MutableDiagnosticsGate : IFlowRuntimeDiagnosticsGate
        {
            private int _enabled;

            internal bool Enabled
            {
                get { return Volatile.Read(ref _enabled) != 0; }
                set { Volatile.Write(ref _enabled, value ? 1 : 0); }
            }

            public bool IsNodeInputCaptureEnabled
            {
                get { return Volatile.Read(ref _enabled) != 0; }
            }
        }

        private sealed class DiagnosticsHarness
        {
            internal IFlowRunner Runner { get; set; }
            internal InMemoryFlowEventSink Sink { get; set; }
            internal NodeDefinition Node { get; set; }
        }

        private sealed class DiagnosticsNodeFactory : INodeFactory
        {
            internal const string TypeName = "test.runtime-diagnostics";
            private readonly Func<int, FlowExecutionContext, NodeExecutionResult> _behavior;
            private int _attempt;

            internal DiagnosticsNodeFactory(
                Func<int, FlowExecutionContext, NodeExecutionResult> behavior)
            {
                _behavior = behavior;
            }

            public string NodeType { get { return TypeName; } }

            public NodeDescriptor Descriptor
            {
                get
                {
                    return new NodeDescriptor
                    {
                        NodeType = TypeName,
                        DisplayName = "运行诊断测试节点",
                        Category = "测试",
                        Version = "1.0.0"
                    };
                }
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return new DiagnosticsNode(this);
            }

            private sealed class DiagnosticsNode : IFlowNode
            {
                private readonly DiagnosticsNodeFactory _owner;

                internal DiagnosticsNode(DiagnosticsNodeFactory owner)
                {
                    _owner = owner;
                }

                public Task<NodeExecutionResult> ExecuteAsync(
                    FlowExecutionContext context,
                    CancellationToken cancellationToken)
                {
                    int attempt = Interlocked.Increment(ref _owner._attempt);
                    return Task.FromResult(_owner._behavior(attempt, context));
                }
            }
        }
    }
}
