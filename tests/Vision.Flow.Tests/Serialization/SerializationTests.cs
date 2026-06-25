using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // Serialization tests protect flow file round-tripping and designer/runtime separation.
    internal static class SerializationTests
    {
        public static Task RuntimeRoundTrip()
        {
            var runtime = CreateSampleRuntime();
            var json = RuntimeFlowSerializer.Serialize(runtime);
            var restored = RuntimeFlowSerializer.Deserialize(json);

            AssertEx.Equal("Station01_Main", restored.FlowId, "Runtime FlowId should round-trip.");
            AssertEx.Equal(2, restored.Nodes.Count, "Runtime nodes should round-trip.");
            AssertEx.Equal("camera.soft_trigger", restored.Nodes[0].Type, "Node type should round-trip.");
            AssertEx.Equal("Camera01", Convert.ToString(restored.Nodes[0].Settings["CameraId"]), "Node settings should round-trip.");
            AssertEx.Equal("camera_trigger_1.Image", restored.Nodes[1].InputBindings["Image"].GetVariableName(), "Input binding should round-trip.");
            AssertEx.Equal(1, restored.Edges.Count, "Runtime edges should round-trip.");
            AssertEx.Equal("ManualStart", restored.Entries[0].EntryName, "Runtime entry should round-trip.");
            AssertEx.False(json.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view zoom.");
            AssertEx.False(json.IndexOf("OffsetX", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view offsets.");
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
                    OffsetY = 34
                }
            };
            document.View.Nodes["camera_trigger_1"] = new NodeViewState
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
            AssertEx.Equal(100.0, restored.View.Nodes["camera_trigger_1"].X, "Node X should round-trip.");
            AssertEx.True(restored.View.Nodes["camera_trigger_1"].IsCollapsed, "Node collapsed state should round-trip.");
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
                Id = "camera_trigger_1",
                Type = "camera.soft_trigger",
                Name = "Camera Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 1000 }
                }
            });

            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "image_save_1",
                Type = "image.save",
                Name = "Save Image",
                Version = "1.0.0",
                InputBindings =
                {
                    { "Image", VariableBinding.ForVariable("camera_trigger_1", "Image") }
                }
            });

            runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "camera_trigger_1",
                FromPort = "Next",
                ToNodeId = "image_save_1",
                ToPort = "In"
            });

            runtime.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "camera_trigger_1"
            });

            return runtime;
        }
    }
}
