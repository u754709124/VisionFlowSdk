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
    // Frame preprocess nodes prepare streaming frames for later scan grouping.
    public sealed class FramePreprocessNodeConfig
    {
        public FramePreprocessNodeConfig()
        {
            FrameIndex = -1;
            Queue = new AdapterNodeQueueConfig
            {
                QueueName = "frame-preprocess"
            };
        }

        public string ScanGroupId { get; set; }

        public string ScanGroupIdBinding { get; set; }

        public string FrameIndexBinding { get; set; }

        public string FrameBinding { get; set; }

        public string ImageBinding { get; set; }

        public string FrameIdBinding { get; set; }

        public int FrameIndex { get; set; }

        public AdapterNodeQueueConfig Queue { get; set; }
    }

    public sealed class FramePreprocessNodeFactory : BaseNodeFactory<FramePreprocessNodeConfig>
    {
        public const string TypeName = "frame.preprocess";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return FramePreprocessNodeDescriptor.Create(); }
        }

        protected override FramePreprocessNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new FramePreprocessNodeConfig
            {
                ScanGroupId = GetStringSetting(definition, "ScanGroupId", null),
                ScanGroupIdBinding = GetStringSetting(definition, "ScanGroupIdBinding", null),
                FrameIndexBinding = GetStringSetting(definition, "FrameIndexBinding", null),
                FrameBinding = GetStringSetting(definition, "FrameBinding", null),
                ImageBinding = GetStringSetting(definition, "ImageBinding", null),
                FrameIdBinding = GetStringSetting(definition, "FrameIdBinding", null),
                FrameIndex = GetInt32Setting(definition, "FrameIndex", -1),
                Queue = AdapterNodeHelpers.CreateQueueConfig(definition, "frame-preprocess")
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, FramePreprocessNodeConfig config)
        {
            return new FramePreprocessNode(config);
        }
    }

    public sealed class FramePreprocessNode : IFlowNode
    {
        private readonly FramePreprocessNodeConfig _config;

        public FramePreprocessNode(FramePreprocessNodeConfig config)
        {
            _config = config ?? new FramePreprocessNodeConfig();
            if (_config.Queue == null)
            {
                _config.Queue = new AdapterNodeQueueConfig { QueueName = "frame-preprocess" };
            }
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanGroupId = ResolveScanGroupId(context, _config.ScanGroupId, _config.ScanGroupIdBinding);
            if (string.IsNullOrWhiteSpace(scanGroupId))
            {
                return NodeExecutionResult.Failure("ScanGroupId is required.");
            }

            int frameIndex;
            if (!TryResolveInt32(context, "FrameIndex", _config.FrameIndexBinding, out frameIndex))
            {
                frameIndex = _config.FrameIndex;
            }

            if (frameIndex < 0)
            {
                return NodeExecutionResult.Failure("FrameIndex must be greater than or equal to zero.");
            }

            var frame = ResolveFrame(context, _config.FrameBinding);
            var sourceImage = frame == null ? ResolveImage(context, _config.ImageBinding) : frame.Image;
            if (sourceImage == null)
            {
                return NodeExecutionResult.Failure("Frame or Image input is required.");
            }

            var queueResult = await AdapterNodeHelpers.ExecuteWithOptionalQueueResultAsync(
                context,
                _config.Queue,
                "frame-preprocess",
                "frame.preprocess",
                delegate(CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(CreatePreprocessResult(context, _config, scanGroupId, frameIndex, frame, sourceImage));
                },
                cancellationToken).ConfigureAwait(false);

            if (!queueResult.WaitedForCompletion)
            {
                return NodeExecutionResult.Success(
                    "Next",
                    new Dictionary<string, object>
                    {
                        { "Queued", queueResult.IsQueued },
                        { "QueueCompleted", false },
                        { "ScanGroupId", scanGroupId },
                        { "FrameIndex", frameIndex }
                    });
            }

            var result = queueResult.Value;
            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "PreprocessResult", result },
                    { "FramePreprocessResult", result },
                    { "PreprocessedImage", result.PreprocessedImage },
                    { "ScanGroupId", scanGroupId },
                    { "FrameIndex", frameIndex },
                    { "FrameId", result.FrameId },
                    { "Queued", false },
                    { "QueueCompleted", true }
                });
        }

        private static FramePreprocessResult CreatePreprocessResult(
            FlowExecutionContext context,
            FramePreprocessNodeConfig config,
            string scanGroupId,
            int frameIndex,
            CameraFrameData frame,
            IVisionImage sourceImage)
        {
            var sourceImageId = sourceImage == null ? null : sourceImage.ImageId;
            var frameId = frame == null ? ResolveStringValue(context, "FrameId", context.Token.FrameId, config.FrameIdBinding) : frame.FrameId;
            if (string.IsNullOrWhiteSpace(frameId))
            {
                frameId = sourceImageId;
            }

            var imageId = "preprocess-" + SafeId(scanGroupId) + "-" + frameIndex.ToString(CultureInfo.InvariantCulture);
            var preprocessedImage = new GeneratedVisionImage(
                imageId,
                sourceImage == null ? 1 : sourceImage.Width,
                sourceImage == null ? 1 : sourceImage.Height,
                sourceImage == null ? "Mono8" : sourceImage.PixelFormat,
                null,
                "Preprocessed");
            preprocessedImage.Metadata["Algorithm"] = "FakeFramePreprocess";
            preprocessedImage.Metadata["ScanGroupId"] = scanGroupId;
            preprocessedImage.Metadata["FrameIndex"] = frameIndex;
            preprocessedImage.Metadata["SourceImageId"] = sourceImageId;
            preprocessedImage.Metadata["FrameId"] = frameId;

            var result = new FramePreprocessResult
            {
                ScanGroupId = scanGroupId,
                FrameIndex = frameIndex,
                SourceFrame = frame,
                SourceImage = sourceImage,
                PreprocessedImage = preprocessedImage,
                FrameId = frameId,
                ProcessedAtUtc = DateTime.UtcNow
            };

            CopyTokenMetadata(context.Token, result.Metadata);
            if (frame != null)
            {
                CopyDictionary(frame.Metadata, result.Metadata);
            }

            result.Metadata["Algorithm"] = "FakeFramePreprocess";
            result.Metadata["ScanGroupId"] = scanGroupId;
            result.Metadata["FrameIndex"] = frameIndex;
            result.Metadata["SourceImageId"] = sourceImageId;
            result.Metadata["FrameId"] = frameId;
            return result;
        }
    }

    public static class FramePreprocessNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = FramePreprocessNodeFactory.TypeName,
                DisplayName = "Frame Preprocess",
                Category = "Algorithm",
                Version = "1.0.0",
                Description = "Creates a fake preprocessing result for a scan frame.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes missing scan group, frame index, or image input.")
                },
                Settings =
                {
                    CreateStringSetting("ScanGroupId", "Scan Group", null, false, "Scan group id. This can also come from token or input binding."),
                    CreateStringSetting("ScanGroupIdBinding", "Scan Group Binding", null, false, "Optional binding expression for ScanGroupId."),
                    CreateStringSetting("FrameIndexBinding", "Frame Index Binding", null, false, "Optional binding expression for FrameIndex."),
                    CreateStringSetting("FrameBinding", "Frame Binding", null, false, "Optional binding expression for Frame."),
                    CreateStringSetting("ImageBinding", "Image Binding", null, false, "Optional binding expression for Image."),
                    CreateStringSetting("FrameIdBinding", "Frame Id Binding", null, false, "Optional binding expression for FrameId."),
                    CreateIntSetting("FrameIndex", "Frame Index", -1, false, "Frame index. Values less than zero require an input or token value."),
                    AdapterNodeDescriptors.QueueUseSetting(),
                    AdapterNodeDescriptors.QueueNameSetting("frame-preprocess"),
                    AdapterNodeDescriptors.QueueCapacitySetting(),
                    AdapterNodeDescriptors.QueueMaxDegreeSetting(),
                    AdapterNodeDescriptors.QueueFullModeSetting(),
                    AdapterNodeDescriptors.QueueWaitForCompletionSetting()
                },
                Outputs =
                {
                    CreateOutput("PreprocessResult", "Preprocess Result", "FramePreprocessResult", "Frame preprocessing result."),
                    CreateOutput("FramePreprocessResult", "Frame Preprocess Result", "FramePreprocessResult", "Alias for PreprocessResult."),
                    CreateOutput("PreprocessedImage", "Preprocessed Image", "IVisionImage", "Fake preprocessed image."),
                    CreateOutput("ScanGroupId", "Scan Group", "String", "Scan group id."),
                    CreateOutput("FrameIndex", "Frame Index", "Int32", "Frame index."),
                    CreateOutput("FrameId", "Frame Id", "String", "Source frame id."),
                    CreateOutput("Queued", "Queued", "Boolean", "True when preprocessing was queued without waiting."),
                    CreateOutput("QueueCompleted", "Queue Completed", "Boolean", "True when queued preprocessing completed before node output.")
                }
            };
        }
    }
}
