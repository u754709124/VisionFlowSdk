using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;
using static Vision.Flow.Nodes.GroupScanFusionNodeHelpers;

namespace Vision.Flow.Nodes
{
    // 拼图节点将已完成的图像组转换为生成的拼接图像。
    public sealed class StitchNodeConfig
    {
        public string FrameGroupBinding { get; set; }
    }

    public sealed class StitchNodeFactory : BaseNodeFactory<StitchNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.ImageStitch;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return StitchNodeDescriptor.Create(); }
        }

        protected override StitchNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new StitchNodeConfig
            {
                FrameGroupBinding = GetStringSetting(definition, "FrameGroupBinding", null)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, StitchNodeConfig config)
        {
            return new StitchNode(config);
        }
    }

    public sealed class StitchNode : IFlowNode
    {
        private readonly StitchNodeConfig _config;

        public StitchNode(StitchNodeConfig config)
        {
            _config = config ?? new StitchNodeConfig();
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frameGroup = ResolveFrameGroup(context, _config.FrameGroupBinding);
            if (frameGroup == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("FrameGroup input is required."));
            }

            if (frameGroup.Frames == null || frameGroup.Frames.Count == 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("FrameGroup must contain at least one frame."));
            }

            var image = CreateStitchedImage(frameGroup);
            return Task.FromResult(NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "StitchedImage", image },
                    { "FrameGroup", frameGroup },
                    { "CaptureGroupId", frameGroup.CaptureGroupId },
                    { "SourceFrameCount", frameGroup.Frames.Count }
                }));
        }

        private static FrameGroupResult ResolveFrameGroup(FlowExecutionContext context, string bindingExpression)
        {
            var value = ResolveConfiguredValue(context, "FrameGroup", bindingExpression);
            if (value == null)
            {
                value = context.GetInputValue("FrameGroupResult");
            }

            return value as FrameGroupResult;
        }

        private static IVisionImage CreateStitchedImage(FrameGroupResult frameGroup)
        {
            var images = frameGroup.Frames
                .Where(x => x != null && x.Image != null)
                .Select(x => x.Image)
                .ToList();
            var first = images.FirstOrDefault();
            var width = images.Count == 0 ? 1 : images.Sum(x => x.Width);
            var height = images.Count == 0 ? 1 : images.Max(x => x.Height);
            var pixelFormat = first == null ? "Mono8" : first.PixelFormat;
            var imageId = "stitch-" + SafeId(frameGroup.CaptureGroupId);
            var image = new GeneratedVisionImage(imageId, width, height, pixelFormat, null, "Stitched");

            image.Metadata[FlowMetadataKeys.Algorithm] = "FakeStitch";
            image.Metadata[FlowMetadataKeys.CaptureGroupId] = frameGroup.CaptureGroupId;
            image.Metadata[FlowMetadataKeys.SourceFrameCount] = frameGroup.Frames.Count;
            image.Metadata[FlowMetadataKeys.ShotIndexes] = string.Join(",", frameGroup.Frames.Select(x => x.ShotIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
            return image;
        }
    }

    public static class StitchNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = StitchNodeFactory.TypeName,
                DisplayName = "Image Stitch",
                Category = "Algorithm",
                Version = "1.0.0",
                Description = "Creates a fake stitched image from a completed frame group.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes missing or invalid frame groups.")
                },
                Settings =
                {
                    CreateStringSetting("FrameGroupBinding", "Frame Group Binding", null, false, "Optional binding expression used when FrameGroup is not bound directly.")
                },
                Outputs =
                {
                    CreateOutput("StitchedImage", "Stitched Image", "IVisionImage", "Fake stitched image."),
                    CreateOutput("FrameGroup", "Frame Group", "FrameGroupResult", "Input frame group."),
                    CreateOutput("CaptureGroupId", "Capture Group", "String", "Frame group id."),
                    CreateOutput("SourceFrameCount", "Source Frames", "Int32", "Number of stitched source frames.")
                }
            };
        }
    }
}
