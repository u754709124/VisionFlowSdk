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
    // 搴忓垪鍖栨祴璇曚繚鎶ゆ祦绋嬫枃浠跺線杩斿拰璁捐鎬?杩愯鎬佸垎绂汇€?
    internal static class SerializationTests
    {
        public static Task RuntimeRoundTrip()
        {
            var runtime = CreateSampleRuntime();
            var json = RuntimeFlowSerializer.Serialize(runtime);
            var restored = RuntimeFlowSerializer.Deserialize(json);

            AssertEx.Equal("Station01_Main", restored.FlowId, "Runtime FlowId should round-trip.");
            AssertEx.Equal(2, restored.Nodes.Count, "Runtime nodes should round-trip.");
            AssertEx.Equal(FlowNodeTypes.VariableSet, restored.Nodes[0].Type, "Node type should round-trip.");
            AssertEx.Equal("Inspection.Result", Convert.ToString(restored.Nodes[0].Settings["VariableName"].ConstantValue), "Constant node settings should round-trip.");
            AssertEx.Equal(NodeSettingValueMode.Variable, restored.Nodes[1].Settings["Message"].Mode, "Variable setting mode should round-trip.");
            AssertEx.Equal("set_result", restored.Nodes[1].Settings["Message"].Selector.Path[0], "Variable source node should round-trip.");
            AssertEx.False(json.IndexOf("InputBindings", StringComparison.OrdinalIgnoreCase) >= 0, "V2 runtime JSON must not contain InputBindings.");
            AssertEx.Equal(1, restored.Edges.Count, "Runtime edges should round-trip.");
            AssertEx.Equal("ManualStart", restored.Entries[0].EntryName, "Runtime entry should round-trip.");
            AssertEx.False(json.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view zoom.");
            AssertEx.False(json.IndexOf("OffsetX", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view offsets.");
            AssertEx.False(json.IndexOf("CanvasWidth", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view canvas width.");
            AssertEx.False(json.IndexOf("CanvasHeight", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view canvas height.");
            AssertEx.False(json.IndexOf("NodeViewState", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain designer view types.");
            return Task.FromResult(0);
        }

        public static Task DesignRoundTrip()
        {
            var document = new FlowDesignDocument
            {
                FlowId = "Station01_Main",
                FlowName = "Station01 Main Flow",
                Runtime = CreateSampleRuntime(),
                View = new FlowViewState
                {
                    Zoom = 1.25,
                    OffsetX = 12,
                    OffsetY = 34,
                    CanvasWidth = 2400,
                    CanvasHeight = 1600
                }
            };
            document.View.Nodes["set_result"] = new NodeViewState
            {
                X = 100,
                Y = 200,
                IsCollapsed = true
            };

            var json = FlowDesignSerializer.Serialize(document);
            var restored = FlowDesignSerializer.Deserialize(json);

            AssertEx.Equal("Station01_Main", restored.FlowId, "Design FlowId should round-trip.");
            AssertEx.Equal("Station01_Main", restored.Runtime.FlowId, "Design runtime should round-trip.");
            AssertEx.Equal(1.25, restored.View.Zoom, "View zoom should round-trip.");
            AssertEx.Equal(2400.0, restored.View.CanvasWidth, "View canvas width should round-trip.");
            AssertEx.Equal(1600.0, restored.View.CanvasHeight, "View canvas height should round-trip.");
            AssertEx.Equal(100.0, restored.View.Nodes["set_result"].X, "Node X should round-trip.");
            AssertEx.True(restored.View.Nodes["set_result"].IsCollapsed, "Node collapsed state should round-trip.");
            return Task.FromResult(0);
        }

        public static Task DesignV1IsRejected()
        {
            var json = "{\"FlowId\":\"legacy-design\",\"FlowName\":\"Legacy Design\",\"SchemaVersion\":1,\"Runtime\":{\"FlowId\":\"legacy-design\",\"FlowName\":\"Legacy Design\",\"SchemaVersion\":1,\"Nodes\":[],\"Edges\":[],\"Entries\":[]},\"View\":{\"Zoom\":1,\"OffsetX\":0,\"OffsetY\":0,\"Nodes\":{}}}";

            AssertEx.Throws<UnsupportedFlowSchemaVersionException>(
                () => FlowDesignSerializer.Deserialize(json),
                "V1 design files should be rejected explicitly.");
            return Task.FromResult(0);
        }

        public static Task RuntimeV1IsRejected()
        {
            var json = "{\"FlowId\":\"legacy-runtime\",\"SchemaVersion\":1,\"Nodes\":[],\"Edges\":[],\"Entries\":[]}";
            AssertEx.Throws<UnsupportedFlowSchemaVersionException>(
                () => RuntimeFlowSerializer.Deserialize(json),
                "V1 runtime files should be rejected explicitly.");
            return Task.FromResult(0);
        }

        public static Task RuntimeEnumSettingsSerializeAsWireStrings()
        {
            var runtime = new RuntimeFlowDefinition
            {
                FlowId = "enum-settings",
                FlowName = "Enum Settings",
                Version = "1.0.0"
            };

            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "condition1",
                Type = FlowNodeTypes.ConditionIf,
                Name = "Condition",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.LeftBinding, NodeSettingValue.ForVariable(VariableSelector.ForToken("PositionId")) },
                    { FlowSettingNames.Operator, NodeSettingValue.ForConstant(ConditionOperator.Equal) },
                    { FlowSettingNames.RightValue, NodeSettingValue.ForConstant("P01") }
                }
            });
            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = FlowNodeTypes.JoinAnd,
                Name = "Join",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.JoinKeyBinding, NodeSettingValue.ForVariable(VariableSelector.ForToken("PositionId")) },
                    { FlowSettingNames.ExpectedInputCount, NodeSettingValue.ForConstant(2) },
                    { FlowSettingNames.TimeoutMs, NodeSettingValue.ForConstant(0) },
                    { FlowSettingNames.DuplicatePolicy, NodeSettingValue.ForConstant(FlowDuplicatePolicy.Ignore) }
                }
            });
            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "log1",
                Type = FlowNodeTypes.LogWrite,
                Name = "Log",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.Level, NodeSettingValue.ForConstant(FlowLogLevel.Warning) },
                    { FlowSettingNames.Message, NodeSettingValue.ForConstant("enum serialization") }
                }
            });

            var json = RuntimeFlowSerializer.Serialize(runtime);
            var restored = RuntimeFlowSerializer.Deserialize(json);

            AssertEx.True(json.IndexOf("\"ConstantValue\":\"Equal\"", StringComparison.OrdinalIgnoreCase) >= 0, "Operator enum should serialize as a wire string.");
            AssertEx.True(json.IndexOf("\"ConstantValue\":\"Ignore\"", StringComparison.OrdinalIgnoreCase) >= 0, "DuplicatePolicy enum should serialize as a wire string.");
            AssertEx.True(json.IndexOf("\"ConstantValue\":\"Warning\"", StringComparison.OrdinalIgnoreCase) >= 0, "Log level enum should serialize as a wire string.");
            AssertEx.False(json.IndexOf("\"Operator\":0", StringComparison.OrdinalIgnoreCase) >= 0, "Operator enum must not serialize as a number.");
            AssertEx.Equal("Equal", Convert.ToString(restored.Nodes[0].Settings[FlowSettingNames.Operator].ConstantValue, CultureInfo.InvariantCulture), "Operator wire value should deserialize as the file string.");
            AssertEx.Equal("Ignore", Convert.ToString(restored.Nodes[1].Settings[FlowSettingNames.DuplicatePolicy].ConstantValue, CultureInfo.InvariantCulture), "DuplicatePolicy wire value should deserialize as the file string.");
            AssertEx.Equal("Warning", Convert.ToString(restored.Nodes[2].Settings[FlowSettingNames.Level].ConstantValue, CultureInfo.InvariantCulture), "Log level wire value should deserialize as the file string.");
            return Task.FromResult(0);
        }

        private static RuntimeFlowDefinition CreateSampleRuntime()
        {
            var runtime = new RuntimeFlowDefinition
            {
                FlowId = "Station01_Main",
                FlowName = "Station01 Main Flow",
                Version = "1.0.0"
            };

            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "set_result",
                Type = FlowNodeTypes.VariableSet,
                Name = "Set Result",
                Version = "1.0.0",
                Settings =
                {
                    { "VariableName", NodeSettingValue.ForConstant("Inspection.Result") },
                    { "Value", NodeSettingValue.ForConstant("OK") }
                }
            });

            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "log_result",
                Type = FlowNodeTypes.LogWrite,
                Name = "Log Result",
                Version = "1.0.0",
                Settings =
                {
                    { "Message", NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("set_result", "Value")) }
                }
            });

            runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "set_result",
                FromPort = "Next",
                ToNodeId = "log_result",
                ToPort = "In"
            });

            runtime.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "set_result"
            });

            return runtime;
        }
    }
}
