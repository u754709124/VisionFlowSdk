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
    // 通用节点测试放在一起，覆盖共享注册和简单节点行为。
    internal static class CommonNodeTests
    {
        public static Task RegisterAllResolvesFactories()
        {
            var registry = new NodeRegistry();

            CommonNodeRegistration.RegisterAll(registry);

            AssertFactoryRegistered(registry, LogNodeFactory.TypeName);
            AssertFactoryRegistered(registry, DelayNodeFactory.TypeName);
            AssertFactoryRegistered(registry, SplitNodeFactory.TypeName);
            AssertFactoryRegistered(registry, VariableSetNodeFactory.TypeName);
            AssertFactoryRegistered(registry, AndJoinNodeFactory.TypeName);
            AssertFactoryRegistered(registry, ConditionNodeFactory.TypeName);
            return Task.FromResult(0);
        }

        public static Task CommonDescriptorsUseStrongEnumTypes()
        {
            var delay = DelayNodeDescriptor.Create();
            AssertEx.Equal(FlowPortDirection.Input, delay.InputPorts[0].Direction, "Input port direction should be strongly typed.");
            AssertEx.Equal(FlowDataType.Control, delay.InputPorts[0].DataType, "Input port data type should be strongly typed.");
            AssertEx.Equal(FlowPortDirection.Output, delay.OutputPorts[0].Direction, "Output port direction should be strongly typed.");
            AssertEx.Equal(FlowDataType.Int32, delay.Settings[0].DataType, "DelayMs setting data type should be strongly typed.");
            AssertEx.Equal(FlowDataType.Int32, delay.Outputs[0].DataType, "DelayMs output data type should be strongly typed.");

            var log = LogNodeDescriptor.Create();
            AssertEx.Equal(FlowDataType.String, log.Settings[0].DataType, "Log level setting still edits as a string wire value.");
            AssertEx.Equal(FlowLogLevel.Info, new LogNodeConfig().Level, "Log config should use a strongly typed default level.");

            var condition = ConditionNodeDescriptor.Create();
            AssertEx.Equal(FlowDataType.Boolean, condition.Outputs[0].DataType, "Condition result output should be strongly typed.");
            AssertEx.Equal(ConditionOperator.Equal, new ConditionNodeConfig().Operator, "Condition config should use a strongly typed default operator.");
            AssertEx.Equal(FlowDuplicatePolicy.Ignore, new AndJoinNodeConfig().DuplicatePolicy, "AND Join config should use a strongly typed default duplicate policy.");
            return Task.FromResult(0);
        }

        public static async Task LogNodeAcceptsStrongEnumLevel()
        {
            var sink = new InMemoryFlowEventSink();
            var runner = CreateCommonRunner(CreateSingleCommonNodeFlow(
                "log1",
                LogNodeFactory.TypeName,
                delegate(NodeDefinition node)
                {
                    node.Settings[FlowSettingNames.Level] = FlowLogLevel.Warning;
                    node.Settings[FlowSettingNames.Message] = "Part reached station.";
                }),
                sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-log-enum" }).ConfigureAwait(false);

            var logEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeCompleted &&
                string.Equals(x.NodeId, "log1", StringComparison.OrdinalIgnoreCase) &&
                x.Data.ContainsKey(FlowRuntimeDataKeys.LogLevel));

            AssertEx.NotNull(logEvent, "LogNode should publish a runtime event for enum log levels.");
            AssertEx.Equal("Warning", Convert.ToString(logEvent.Data[FlowRuntimeDataKeys.LogLevel], CultureInfo.InvariantCulture), "Log event should publish the enum wire value.");
        }

        public static async Task LogNodePublishesRuntimeEvent()
        {
            var sink = new InMemoryFlowEventSink();
            var runner = CreateCommonRunner(CreateSingleCommonNodeFlow(
                "log1",
                LogNodeFactory.TypeName,
                delegate(NodeDefinition node)
                {
                    node.Settings["Level"] = "Info";
                    node.Settings["Message"] = "Part reached station.";
                }),
                sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-log" }).ConfigureAwait(false);

            var logEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeCompleted &&
                string.Equals(x.NodeId, "log1", StringComparison.OrdinalIgnoreCase) &&
                x.Data.ContainsKey(FlowRuntimeDataKeys.Kind) &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.Kind]), "Log", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(logEvent, "LogNode should publish a runtime event marked as a log.");
            AssertEx.Equal("Part reached station.", logEvent.Message, "Log event message should match the configured message.");
            AssertEx.Equal("Info", Convert.ToString(logEvent.Data[FlowRuntimeDataKeys.LogLevel]), "Log event should include the configured log level.");
        }

        public static async Task DelayNodeExecutes()
        {
            var sink = new InMemoryFlowEventSink();
            var runner = CreateCommonRunner(CreateSingleCommonNodeFlow(
                "delay1",
                DelayNodeFactory.TypeName,
                delegate(NodeDefinition node)
                {
                    node.Settings["DelayMs"] = 1;
                }),
                sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-delay" }).ConfigureAwait(false);

            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.NodeCompleted &&
                    string.Equals(x.NodeId, "delay1", StringComparison.OrdinalIgnoreCase) &&
                    x.OutputPort == "Next"),
                "DelayNode should complete through the Next port.");
            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.OutputProduced &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), "delay1.DelayMs", StringComparison.OrdinalIgnoreCase) &&
                    object.Equals(1, x.Data[FlowRuntimeDataKeys.Value])),
                "DelayNode should publish the resolved DelayMs output.");
        }

        public static async Task VariableSetNodeWritesVariableForNextNode()
        {
            var sink = new InMemoryFlowEventSink();
            var executionLog = new List<string>();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new RecordingNodeFactory(executionLog));

            var flow = new RuntimeFlowDefinition
            {
                FlowId = "variable-set",
                FlowName = "Variable Set",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "set1",
                Type = VariableSetNodeFactory.TypeName,
                Name = "Set Shared Variable",
                Version = "1.0.0",
                Settings =
                {
                    { "VariableName", "Shared.Value" },
                    { "Value", "station-ok" }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "reader1",
                Type = RecordingNodeFactory.TypeName,
                Name = "Read Shared Variable",
                Version = "1.0.0",
                Settings =
                {
                    { "RequiredVariable", "Shared.Value" }
                }
            });

            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "set1",
                FromPort = "Next",
                ToNodeId = "reader1",
                ToPort = "In"
            });
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "set1" });

            var runner = new FlowEngine(registry, sink).CreateRunner(flow);
            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-variable" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "reader1" }, executionLog, "Subsequent node should execute after VariableSetNode.");
            AssertEx.False(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeFailed),
                "VariableSetNode should write the named variable before the next node reads it.");
            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.OutputProduced &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), "set1.Value", StringComparison.OrdinalIgnoreCase) &&
                    object.Equals("station-ok", x.Data[FlowRuntimeDataKeys.Value])),
                "VariableSetNode should also publish the written value as a node output.");
        }

        private static void AssertFactoryRegistered(NodeRegistry registry, string nodeType)
        {
            INodeFactory factory;
            AssertEx.True(registry.TryGetFactory(nodeType, out factory), nodeType + " factory should be registered.");
            AssertEx.NotNull(factory.Descriptor, nodeType + " descriptor should be available.");
            AssertEx.Equal(nodeType, factory.Descriptor.NodeType, nodeType + " descriptor should use the registered node type.");
        }

        private static IFlowRunner CreateCommonRunner(RuntimeFlowDefinition flow, InMemoryFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateSingleCommonNodeFlow(string nodeId, string nodeType, Action<NodeDefinition> configure)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = nodeId + "-flow",
                FlowName = nodeId + " Flow",
                Version = "1.0.0"
            };

            var node = new NodeDefinition
            {
                Id = nodeId,
                Type = nodeType,
                Name = nodeId,
                Version = "1.0.0"
            };

            if (configure != null)
            {
                configure(node);
            }

            flow.Nodes.Add(node);
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = nodeId });
            return flow;
        }
    }
}
