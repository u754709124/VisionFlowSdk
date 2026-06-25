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
    // 相机节点测试集中覆盖触发、回调和流式帧场景。
    internal static class CameraNodeTests
    {
        public static async Task SetTriggerCallbackFlow()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 20
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var runner = CreateCameraRunner(CreateSetTriggerCallbackFlow(), sink, devices, null);
            var token = new FlowToken { TokenId = "token-camera-chain" };

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);

            var exposure = await camera.GetParameterAsync("ExposureTime", CancellationToken.None).ConfigureAwait(false);
            var gain = await camera.GetParameterAsync("Gain", CancellationToken.None).ConfigureAwait(false);
            AssertEx.Equal(1234.5, Convert.ToDouble(exposure), "CameraSetParameterNode should set ExposureTime.");
            AssertEx.Equal(2.5, Convert.ToDouble(gain), "CameraSetParameterNode should set Gain.");

            var triggerId = Convert.ToString(FindOutput(sink, "trigger1", "TriggerId"));
            var frame = FindOutput(sink, "callback1", "Frame") as CameraFrameData;
            var image = FindOutput(sink, "callback1", "Image") as IVisionImage;
            var frameId = Convert.ToString(FindOutput(sink, "callback1", "FrameId"));
            var metadata = FindOutput(sink, "callback1", "Metadata") as IDictionary<string, object>;

            AssertEx.NotNull(frame, "CameraImageCallbackNode should output the matched frame.");
            AssertEx.NotNull(image, "CameraImageCallbackNode should output the matched image.");
            AssertEx.NotNull(metadata, "CameraImageCallbackNode should output frame metadata.");
            AssertEx.Equal("Camera01", frame.CameraId, "Callback frame camera id should match the configured camera.");
            AssertEx.Equal(triggerId, frame.TriggerId, "Callback frame should match the soft trigger id.");
            AssertEx.Equal(frame.FrameId, frameId, "FrameId output should match the frame.");
            AssertEx.Equal(frame.FrameId, image.ImageId, "Fake image id should match the frame id.");
            AssertEx.Equal(triggerId, Convert.ToString(metadata["TriggerId"]), "Frame metadata should carry the trigger id.");
            AssertEx.Equal(triggerId, token.Get<string>("TriggerId"), "Soft trigger should write TriggerId to the token.");
            AssertEx.Equal(frame.FrameId, token.FrameId, "Image callback should write FrameId to the token.");
        }

        public static async Task ImageCallbackTimeoutWhenTriggerIdDoesNotMatch()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var executionLog = new List<string>();
            var runner = CreateCameraRunner(CreateMismatchedTriggerFlow(), sink, devices, executionLog);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-camera-timeout" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "TimeoutHandler" }, executionLog, "Mismatched TriggerId should route through the Timeout output.");
            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.NodeTimeout &&
                    string.Equals(x.NodeId, "callback1", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.OutputPort, "Timeout", StringComparison.OrdinalIgnoreCase)),
                "CameraImageCallbackNode should publish a NodeTimeout event.");
            AssertEx.False(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.OutputProduced &&
                    string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), "callback1.Image", StringComparison.OrdinalIgnoreCase)),
                "Timed out callback should not publish image outputs.");
        }

        public static async Task ImageCallbackAnyMatchMode()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var runner = CreateCameraRunner(CreateAnyMatchFlow(), sink, devices, null);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-camera-any" }).ConfigureAwait(false);

            var triggerId = Convert.ToString(FindOutput(sink, "trigger1", "TriggerId"));
            var frame = FindOutput(sink, "callback1", "Frame") as CameraFrameData;

            AssertEx.NotNull(frame, "Any match mode should output the first available frame.");
            AssertEx.Equal("Camera01", frame.CameraId, "Any match frame camera id should match.");
            AssertEx.Equal(triggerId, frame.TriggerId, "Any match should consume the soft trigger frame.");
        }

        public static async Task ImageCallbackStreamFrames()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var runner = CreateCameraRunner(CreateStreamFramesFlow(), sink, devices, null);

            await runner.StartAsync().ConfigureAwait(false);
            var triggerTask = runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-camera-stream" });

            await Task.Delay(20).ConfigureAwait(false);
            await camera.SoftTriggerAsync(
                new CameraTriggerContext { CameraId = "Camera01", TriggerId = "stream-001" },
                CancellationToken.None).ConfigureAwait(false);
            await camera.SoftTriggerAsync(
                new CameraTriggerContext { CameraId = "Camera01", TriggerId = "stream-002" },
                CancellationToken.None).ConfigureAwait(false);

            await triggerTask.ConfigureAwait(false);

            var frames = FindOutput(sink, "callback1", "Frames") as IList<CameraFrameData>;
            var frameCount = Convert.ToInt32(FindOutput(sink, "callback1", "FrameCount"), CultureInfo.InvariantCulture);
            var lastTriggerId = Convert.ToString(FindOutput(sink, "callback1", "TriggerId"));

            AssertEx.NotNull(frames, "StreamFrames should output the collected frame list.");
            AssertEx.Equal(2, frames.Count, "StreamFrames should collect the configured frame count.");
            AssertEx.Equal(2, frameCount, "FrameCount output should match the collected frame count.");
            AssertEx.Equal("stream-001", frames[0].TriggerId, "First stream frame should preserve trigger id.");
            AssertEx.Equal("stream-002", frames[1].TriggerId, "Second stream frame should preserve trigger id.");
            AssertEx.Equal("stream-002", lastTriggerId, "Node scalar outputs should describe the last collected frame.");
        }

        public static async Task ImageCallbackStreamFramesPerFrame()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var executionLog = new List<string>();
            var runner = CreateCameraRunner(CreateStreamFramesPerFrameFlow(), sink, devices, executionLog);

            await runner.StartAsync().ConfigureAwait(false);
            var triggerTask = runner.TriggerAsync(
                "ManualStart",
                new FlowToken
                {
                    TokenId = "token-camera-stream-per-frame",
                    ScanGroupId = "scan-per-frame"
                });

            await Task.Delay(20).ConfigureAwait(false);
            await camera.SoftTriggerAsync(
                new CameraTriggerContext { CameraId = "Camera01", TriggerId = "stream-frame-001" },
                CancellationToken.None).ConfigureAwait(false);
            await camera.SoftTriggerAsync(
                new CameraTriggerContext { CameraId = "Camera01", TriggerId = "stream-frame-002" },
                CancellationToken.None).ConfigureAwait(false);

            await triggerTask.ConfigureAwait(false);

            AssertEx.Equal(2, executionLog.Count(x => string.Equals(x, "recordFrame", StringComparison.OrdinalIgnoreCase)), "PerFrame stream mode should dispatch every frame to downstream nodes.");
            var frameIndexes = sink.Events
                .Where(x => x.EventType == FlowRuntimeEventType.OutputProduced && string.Equals(x.NodeId, "callback1", StringComparison.OrdinalIgnoreCase))
                .Where(x => string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), "callback1.FrameIndex", StringComparison.OrdinalIgnoreCase))
                .Select(x => Convert.ToInt32(x.Data[FlowRuntimeDataKeys.Value], CultureInfo.InvariantCulture))
                .ToList();
            AssertEx.SequenceEqual(new[] { 0, 1 }, frameIndexes, "PerFrame stream mode should produce incrementing FrameIndex values.");

            var completed = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeCompleted &&
                string.Equals(x.NodeId, "callback1", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.OutputPort, "Completed", StringComparison.OrdinalIgnoreCase));
            AssertEx.NotNull(completed, "PerFrame stream mode should complete through the Completed output port.");
        }

        private static IFlowRunner CreateCameraRunner(
            RuntimeFlowDefinition flow,
            InMemoryFlowEventSink sink,
            DefaultDeviceRegistry devices,
            IList<string> executionLog)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            if (executionLog != null)
            {
                registry.Register(new RecordingNodeFactory(executionLog));
            }

            return new FlowEngine(registry, sink, devices).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateSetTriggerCallbackFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-chain",
                FlowName = "Camera Chain",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "set1",
                Type = CameraSetParameterNodeFactory.TypeName,
                Name = "Set Camera Parameters",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 },
                    {
                        "Parameters",
                        new[]
                        {
                            new CameraParameterSetConfig { Name = "ExposureTime", Value = 1234.5 },
                            new CameraParameterSetConfig { Name = "Gain", Value = 2.5 }
                        }
                    }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "MatchMode", "TriggerId" },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "TriggerId", VariableBinding.ForVariable("trigger1", "TriggerId") }
                }
            });

            flow.Edges.Add(CreateEdge("set1", "Next", "trigger1"));
            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "set1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateAnyMatchFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-any",
                FlowName = "Camera Any Match",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "CallbackMode", "WaitNextFrame" },
                    { "MatchMode", "Any" },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "trigger1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateStreamFramesFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-stream",
                FlowName = "Camera Stream",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "CallbackMode", "StreamFrames" },
                    { "MatchMode", "Any" },
                    { "ExpectedFrameCount", 2 },
                    { "FrameTimeoutMs", 1000 },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "callback1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateStreamFramesPerFrameFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-stream-per-frame",
                FlowName = "Camera Stream Per Frame",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "CallbackMode", "StreamFrames" },
                    { "StreamOutputMode", "PerFrame" },
                    { "MatchMode", "Any" },
                    { "ScanGroupIdBinding", "{{ token.ScanGroupId }}" },
                    { "ExpectedFrameCount", 2 },
                    { "FrameTimeoutMs", 1000 },
                    { "TimeoutMs", 1000 },
                    { "StartFrameIndex", 0 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "recordFrame",
                Type = RecordingNodeFactory.TypeName,
                Name = "Record Frame",
                Version = "1.0.0",
                Settings =
                {
                    { "RequiredVariable", "callback1.Frame" }
                }
            });

            flow.Edges.Add(CreateEdge("callback1", "Frame", "recordFrame"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "callback1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateMismatchedTriggerFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-timeout",
                FlowName = "Camera Timeout",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TriggerId", "missing-trigger-id" },
                    { "MatchMode", "TriggerId" },
                    { "TimeoutMs", 50 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "TimeoutHandler",
                Type = RecordingNodeFactory.TypeName,
                Name = "Timeout Handler",
                Version = "1.0.0"
            });

            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Edges.Add(CreateEdge("callback1", "Timeout", "TimeoutHandler"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "trigger1" });
            return flow;
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

        private static object FindOutput(InMemoryFlowEventSink sink, string nodeId, string outputName)
        {
            var variableName = nodeId + "." + outputName;
            var runtimeEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName]), variableName, StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data[FlowRuntimeDataKeys.Value];
        }
    }
}
