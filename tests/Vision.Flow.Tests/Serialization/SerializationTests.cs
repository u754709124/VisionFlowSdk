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
    // 序列化测试保护流程文件往返和设计态/运行态分离。
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
            AssertEx.Equal("Inspection.Result", Convert.ToString(restored.Nodes[0].Settings["VariableName"]), "Node settings should round-trip.");
            AssertEx.Equal("set_result.Value", restored.Nodes[1].InputBindings["Message"].GetVariableName(), "Input binding should round-trip.");
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

        public static Task DesignMissingCanvasSizeUsesDefaults()
        {
            var json = "{\"FlowId\":\"legacy-design\",\"FlowName\":\"Legacy Design\",\"SchemaVersion\":1,\"Runtime\":{\"FlowId\":\"legacy-design\",\"FlowName\":\"Legacy Design\",\"SchemaVersion\":1,\"Nodes\":[],\"Edges\":[],\"Entries\":[]},\"View\":{\"Zoom\":1,\"OffsetX\":0,\"OffsetY\":0,\"Nodes\":{}}}";

            var restored = FlowDesignSerializer.Deserialize(json);

            AssertEx.Equal(FlowViewState.DefaultCanvasWidth, restored.View.CanvasWidth, "Legacy design should use default canvas width.");
            AssertEx.Equal(FlowViewState.DefaultCanvasHeight, restored.View.CanvasHeight, "Legacy design should use default canvas height.");
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
                    { "VariableName", "Inspection.Result" },
                    { "Value", "OK" }
                }
            });

            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "log_result",
                Type = FlowNodeTypes.LogWrite,
                Name = "Log Result",
                Version = "1.0.0",
                InputBindings =
                {
                    { "Message", VariableBinding.ForVariable("set_result", "Value") }
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
