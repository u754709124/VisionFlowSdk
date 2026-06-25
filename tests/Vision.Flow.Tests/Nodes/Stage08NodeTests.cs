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
    // Stage 08 tests cover frame grouping, scan grouping, stitching, and fusion behavior.
    internal static class Stage08NodeTests
    {
        public static async Task FrameGroupJoinSortsAndStitches()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateFrameGroupStitchFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-A", 2)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-A", 1)).ConfigureAwait(false);

            var group = FindOutput(sink, "join1", "FrameGroup") as FrameGroupResult;
            var stitched = FindOutput(sink, "stitch1", "StitchedImage") as IVisionImage;

            AssertEx.NotNull(group, "FrameGroupJoinNode should output a completed frame group.");
            AssertEx.Equal("capture-A", group.CaptureGroupId, "FrameGroupResult should keep the capture group id.");
            AssertEx.Equal(2, group.ActualShotCount, "FrameGroupResult should include both frames.");
            AssertEx.SequenceEqual(new[] { 1, 2 }, group.Frames.Select(x => x.ShotIndex), "FrameGroupResult should be sorted by ShotIndex.");
            AssertEx.NotNull(stitched, "StitchNode should output a stitched image.");
            AssertEx.Equal("capture-A", Convert.ToString(stitched.Metadata["CaptureGroupId"]), "Stitched image should carry CaptureGroupId metadata.");
            AssertEx.Equal(2, Convert.ToInt32(stitched.Metadata["SourceFrameCount"]), "Stitched image should record source frame count.");
        }

        public static async Task FrameGroupJoinDetectsDuplicateShotIndex()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateDuplicateFrameGroupFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-duplicate", 1)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-duplicate", 1)).ConfigureAwait(false);

            var failure = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeFailed &&
                string.Equals(x.NodeId, "join1", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(failure, "Duplicate ShotIndex should fail FrameGroupJoinNode.");
            AssertEx.True(
                failure.Message.IndexOf("Duplicate ShotIndex", StringComparison.OrdinalIgnoreCase) >= 0,
                "FrameGroupJoinNode failure should identify the duplicate ShotIndex.");
        }

        public static async Task FrameGroupJoinBindingsReplaceAndContinuousValidation()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateFrameGroupBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-bind", 1)).ConfigureAwait(false);

            var replacement = CreateCaptureToken("capture-bind", 1);
            var replacementFrame = CreateFrame("capture-bind", 1);
            replacementFrame.FrameId = "replacement-shot-1";
            replacementFrame.Image = new FakeVisionImage("replacement-shot-1", 111, 80, "Mono8", null);
            replacement.FrameId = replacementFrame.FrameId;
            replacement.Set("Frame", replacementFrame);
            await runner.TriggerAsync("ManualStart", replacement).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-bind", 2)).ConfigureAwait(false);

            var group = FindOutput(sink, "join1", "FrameGroup") as FrameGroupResult;
            AssertEx.NotNull(group, "FrameGroupJoinNode should complete after a replaced duplicate and the second shot.");
            AssertEx.SequenceEqual(new[] { 1, 2 }, group.Frames.Select(x => x.ShotIndex), "FrameGroupJoinNode should keep continuous ShotIndexes from settings.");
            AssertEx.Equal("replacement-shot-1", group.Frames[0].FrameId, "DuplicatePolicy=Replace should keep the latest duplicate item.");
        }

        public static async Task FrameGroupJoinDetectsNonContinuousShotIndex()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateFrameGroupBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-gap", 1)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-gap", 3)).ConfigureAwait(false);

            var failure = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeFailed &&
                string.Equals(x.NodeId, "join1", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(failure, "Non-continuous ShotIndex values should fail FrameGroupJoinNode when validation is enabled.");
            AssertEx.True(
                failure.Message.IndexOf("continuous", StringComparison.OrdinalIgnoreCase) >= 0,
                "FrameGroupJoinNode failure should identify continuous ShotIndex validation.");
        }

        public static async Task ScanGroupJoinSortsAndFusionOutputsImages()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateScanFusionFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-A", 2)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-A", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-A", 1)).ConfigureAwait(false);

            var scanGroup = FindOutput(sink, "scanJoin1", "ScanGroupResult") as ScanGroupResult;
            var final3D = FindOutput(sink, "fusion1", "Final3DImage") as IVisionImage;
            var final2D = FindOutput(sink, "fusion1", "Final2DImage") as IVisionImage;
            var heightMap = FindOutput(sink, "fusion1", "HeightMap") as IVisionImage;
            var textureImage = FindOutput(sink, "fusion1", "TextureImage") as IVisionImage;
            var confidenceMap = FindOutput(sink, "fusion1", "ConfidenceMap") as IVisionImage;

            AssertEx.NotNull(scanGroup, "ScanGroupJoinNode should output a completed scan group.");
            AssertEx.Equal("scan-A", scanGroup.ScanGroupId, "ScanGroupResult should keep the scan group id.");
            AssertEx.Equal(3, scanGroup.ActualFrameCount, "ScanGroupResult should include all preprocess results.");
            AssertEx.SequenceEqual(new[] { 0, 1, 2 }, scanGroup.Frames.Select(x => x.FrameIndex), "ScanGroupResult should be sorted by FrameIndex.");
            AssertEx.NotNull(final3D, "Final3D2DFusionNode should output Final3DImage.");
            AssertEx.NotNull(final2D, "Final3D2DFusionNode should output Final2DImage.");
            AssertEx.NotNull(heightMap, "Final3D2DFusionNode should output HeightMap.");
            AssertEx.NotNull(textureImage, "Final3D2DFusionNode should output TextureImage.");
            AssertEx.NotNull(confidenceMap, "Final3D2DFusionNode should output ConfidenceMap.");
            AssertEx.True(object.ReferenceEquals(final3D, heightMap), "Final3DImage should remain a HeightMap alias.");
            AssertEx.True(object.ReferenceEquals(final2D, textureImage), "Final2DImage should remain a TextureImage alias.");
            AssertEx.Equal("HeightMap", heightMap.ImageKind, "HeightMap output should carry ImageKind.");
            AssertEx.Equal("TextureImage", textureImage.ImageKind, "TextureImage output should carry ImageKind.");
            AssertEx.Equal("ConfidenceMap", confidenceMap.ImageKind, "ConfidenceMap output should carry ImageKind.");
            AssertEx.Equal("scan-A", Convert.ToString(final3D.Metadata["ScanGroupId"]), "Final3DImage should carry ScanGroupId metadata.");
            AssertEx.Equal("scan-A", Convert.ToString(final2D.Metadata["ScanGroupId"]), "Final2DImage should carry ScanGroupId metadata.");
            AssertEx.Equal(3, Convert.ToInt32(final3D.Metadata["SourceFrameCount"]), "Final3DImage should record source frame count.");
            AssertEx.Equal(3, Convert.ToInt32(final2D.Metadata["SourceFrameCount"]), "Final2DImage should record source frame count.");
        }

        public static async Task ScanGroupJoinBindingsReplaceAndFusionBinding()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateScanFusionBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-bind", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-bind", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-bind", 1)).ConfigureAwait(false);

            var scanGroup = FindOutput(sink, "scanJoin1", "ScanGroupResult") as ScanGroupResult;
            var final3D = FindOutput(sink, "fusion1", "Final3DImage") as IVisionImage;

            AssertEx.NotNull(scanGroup, "ScanGroupJoinNode should complete through PreprocessResultBinding.");
            AssertEx.SequenceEqual(new[] { 0, 1 }, scanGroup.Frames.Select(x => x.FrameIndex), "ScanGroupJoinNode should keep continuous FrameIndexes from settings.");
            AssertEx.NotNull(final3D, "Final3D2DFusionNode should resolve ScanGroupResultBinding.");
            AssertEx.Equal("scan-bind", Convert.ToString(final3D.Metadata["ScanGroupId"]), "Fusion output should carry the bound scan group id.");
        }

        public static async Task ScanGroupJoinDetectsNonContinuousFrameIndex()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateScanFusionBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-gap", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-gap", 2)).ConfigureAwait(false);

            var failure = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeFailed &&
                string.Equals(x.NodeId, "scanJoin1", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(failure, "Non-continuous FrameIndex values should fail ScanGroupJoinNode when validation is enabled.");
            AssertEx.True(
                failure.Message.IndexOf("continuous", StringComparison.OrdinalIgnoreCase) >= 0,
                "ScanGroupJoinNode failure should identify continuous FrameIndex validation.");
        }

        private static RuntimeFlowDefinition CreateFrameGroupStitchFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-frame-group",
                FlowName = "Stage 08 Frame Group",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = FrameGroupJoinNodeFactory.TypeName,
                Name = "Frame Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "ExpectedShotCount", 2 },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "stitch1",
                Type = StitchNodeFactory.TypeName,
                Name = "Stitch",
                Version = "1.0.0",
                InputBindings =
                {
                    { "FrameGroup", VariableBinding.ForVariable("join1", "FrameGroup") }
                }
            });

            flow.Edges.Add(CreateEdge("join1", "Next", "stitch1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateDuplicateFrameGroupFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-frame-group-duplicate",
                FlowName = "Stage 08 Frame Group Duplicate",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = FrameGroupJoinNodeFactory.TypeName,
                Name = "Frame Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "ExpectedShotCount", 3 },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateFrameGroupBindingFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-frame-group-binding",
                FlowName = "Stage 08 Frame Group Binding",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = FrameGroupJoinNodeFactory.TypeName,
                Name = "Frame Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "CaptureGroupIdBinding", "{{ token.CaptureGroupId }}" },
                    { "ShotIndexBinding", "{{ token.ShotIndex }}" },
                    { "FrameBinding", "{{ token.Frame }}" },
                    { "ExpectedShotCount", 2 },
                    { "DuplicatePolicy", "Replace" },
                    { "RequireContinuousShotIndex", true },
                    { "FirstShotIndex", 1 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateScanFusionFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-scan-fusion",
                FlowName = "Stage 08 Scan Fusion",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "preprocess1",
                Type = FramePreprocessNodeFactory.TypeName,
                Name = "Frame Preprocess",
                Version = "1.0.0"
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "scanJoin1",
                Type = ScanGroupJoinNodeFactory.TypeName,
                Name = "Scan Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "ExpectedFrameCount", 3 },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "PreprocessResult", VariableBinding.ForVariable("preprocess1", "PreprocessResult") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "fusion1",
                Type = Final3D2DFusionNodeFactory.TypeName,
                Name = "Final Fusion",
                Version = "1.0.0",
                InputBindings =
                {
                    { "ScanGroupResult", VariableBinding.ForVariable("scanJoin1", "ScanGroupResult") }
                }
            });

            flow.Edges.Add(CreateEdge("preprocess1", "Next", "scanJoin1"));
            flow.Edges.Add(CreateEdge("scanJoin1", "Next", "fusion1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "preprocess1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateScanFusionBindingFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-scan-fusion-binding",
                FlowName = "Stage 08 Scan Fusion Binding",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "preprocess1",
                Type = FramePreprocessNodeFactory.TypeName,
                Name = "Frame Preprocess",
                Version = "1.0.0",
                Settings =
                {
                    { "ScanGroupIdBinding", "{{ token.ScanGroupId }}" },
                    { "FrameIndexBinding", "{{ token.FrameIndex }}" },
                    { "ImageBinding", "{{ token.Image }}" },
                    { "FrameIdBinding", "{{ token.FrameId }}" }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "scanJoin1",
                Type = ScanGroupJoinNodeFactory.TypeName,
                Name = "Scan Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "PreprocessResultBinding", "{{ preprocess1.PreprocessResult }}" },
                    { "ExpectedFrameCount", 2 },
                    { "DuplicatePolicy", "Replace" },
                    { "RequireContinuousFrameIndex", true },
                    { "FirstFrameIndex", 0 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "fusion1",
                Type = Final3D2DFusionNodeFactory.TypeName,
                Name = "Final Fusion",
                Version = "1.0.0",
                Settings =
                {
                    { "ScanGroupResultBinding", "{{ scanJoin1.ScanGroupResult }}" }
                }
            });

            flow.Edges.Add(CreateEdge("preprocess1", "Next", "scanJoin1"));
            flow.Edges.Add(CreateEdge("scanJoin1", "Next", "fusion1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "preprocess1" });
            return flow;
        }

        private static FlowToken CreateCaptureToken(string captureGroupId, int shotIndex)
        {
            var frame = CreateFrame(captureGroupId, shotIndex);
            var token = new FlowToken
            {
                TokenId = "token-" + captureGroupId + "-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                CaptureGroupId = captureGroupId,
                FrameId = frame.FrameId
            };
            token.Set("Frame", frame);
            token.Set("ShotIndex", shotIndex);
            return token;
        }

        private static FlowToken CreateScanToken(string scanGroupId, int frameIndex)
        {
            var image = new FakeVisionImage(
                "scan-" + scanGroupId + "-" + frameIndex.ToString(CultureInfo.InvariantCulture),
                320,
                120,
                "Mono8",
                null);
            var token = new FlowToken
            {
                TokenId = "token-" + scanGroupId + "-" + frameIndex.ToString(CultureInfo.InvariantCulture),
                ScanGroupId = scanGroupId,
                FrameId = image.ImageId
            };
            token.Set("Image", image);
            token.Set("FrameIndex", frameIndex);
            return token;
        }

        private static CameraFrameData CreateFrame(string captureGroupId, int shotIndex)
        {
            var image = new FakeVisionImage(
                "frame-" + captureGroupId + "-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                100 + shotIndex,
                80,
                "Mono8",
                null);
            var frame = new CameraFrameData
            {
                CameraId = "Camera01",
                TriggerId = "trigger-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                FrameId = image.ImageId,
                GrabTime = DateTime.UtcNow,
                Image = image
            };
            frame.Metadata["CaptureGroupId"] = captureGroupId;
            frame.Metadata["ShotIndex"] = shotIndex;
            return frame;
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
                string.Equals(Convert.ToString(x.Data["VariableName"]), variableName, StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data["Value"];
        }
    }
}
