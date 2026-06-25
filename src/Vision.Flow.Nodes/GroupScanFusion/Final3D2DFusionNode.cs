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
    // 最终融合节点基于已完成的扫描组生成最终 3D/2D 图像集合。
    public sealed class Final3D2DFusionNodeConfig
    {
        public Final3D2DFusionNodeConfig()
        {
            Queue = new AdapterNodeQueueConfig
            {
                QueueName = FlowQueueNames.Fusion
            };
        }

        public string ScanGroupResultBinding { get; set; }

        public AdapterNodeQueueConfig Queue { get; set; }
    }

    public sealed class Final3D2DFusionNodeFactory : BaseNodeFactory<Final3D2DFusionNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.FusionFinal3D2D;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return Final3D2DFusionNodeDescriptor.Create(); }
        }

        protected override Final3D2DFusionNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new Final3D2DFusionNodeConfig
            {
                ScanGroupResultBinding = GetStringSetting(definition, "ScanGroupResultBinding", null),
                Queue = AdapterNodeHelpers.CreateQueueConfig(definition, FlowQueueNames.Fusion)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, Final3D2DFusionNodeConfig config)
        {
            return new Final3D2DFusionNode(config);
        }
    }

    public sealed class Final3D2DFusionNode : IFlowNode
    {
        private readonly Final3D2DFusionNodeConfig _config;

        public Final3D2DFusionNode(Final3D2DFusionNodeConfig config)
        {
            _config = config ?? new Final3D2DFusionNodeConfig();
            if (_config.Queue == null)
            {
                _config.Queue = new AdapterNodeQueueConfig { QueueName = FlowQueueNames.Fusion };
            }
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanGroup = ResolveScanGroup(context, _config.ScanGroupResultBinding);
            if (scanGroup == null)
            {
                return NodeExecutionResult.Failure("ScanGroupResult input is required.");
            }

            if (scanGroup.Frames == null || scanGroup.Frames.Count == 0)
            {
                return NodeExecutionResult.Failure("ScanGroupResult must contain at least one frame.");
            }

            var queueResult = await AdapterNodeHelpers.ExecuteWithOptionalQueueResultAsync(
                context,
                _config.Queue,
                FlowQueueNames.Fusion,
                FlowNodeTypes.FusionFinal3D2D,
                delegate(CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(CreateFusionOutputs(scanGroup));
                },
                cancellationToken).ConfigureAwait(false);

            if (!queueResult.WaitedForCompletion)
            {
                return NodeExecutionResult.Success(
                    "Next",
                    new Dictionary<string, object>
                    {
                        { "ScanGroup", scanGroup },
                        { "ScanGroupResult", scanGroup },
                        { "ScanGroupId", scanGroup.ScanGroupId },
                        { "SourceFrameCount", scanGroup.Frames.Count },
                        { "Queued", queueResult.IsQueued },
                        { "QueueCompleted", false }
                    });
            }

            var outputs = queueResult.Value;
            outputs["Queued"] = false;
            outputs["QueueCompleted"] = true;
            return NodeExecutionResult.Success(
                "Next",
                outputs);
        }

        private static ScanGroupResult ResolveScanGroup(FlowExecutionContext context, string bindingExpression)
        {
            var value = ResolveConfiguredValue(context, "ScanGroupResult", bindingExpression);
            if (value == null)
            {
                value = context.GetInputValue("ScanGroup");
            }

            return value as ScanGroupResult;
        }

        private static Dictionary<string, object> CreateFusionOutputs(ScanGroupResult scanGroup)
        {
            var heightMap = CreateFusionImage(scanGroup, "heightmap", "FakeHeightMapFusion", "Float32", "HeightMap");
            var textureImage = CreateFusionImage(scanGroup, "texture", "FakeTextureFusion", null, "TextureImage");
            var confidenceMap = CreateFusionImage(scanGroup, "confidence", "FakeConfidenceFusion", "Float32", "ConfidenceMap");
            return new Dictionary<string, object>
            {
                { "Final3DImage", heightMap },
                { "Final2DImage", textureImage },
                { "HeightMap", heightMap },
                { "TextureImage", textureImage },
                { "ConfidenceMap", confidenceMap },
                { "ScanGroup", scanGroup },
                { "ScanGroupResult", scanGroup },
                { "ScanGroupId", scanGroup.ScanGroupId },
                { "SourceFrameCount", scanGroup.Frames.Count }
            };
        }

        private static IVisionImage CreateFusionImage(ScanGroupResult scanGroup, string prefix, string algorithm, string pixelFormat, string imageKind)
        {
            var images = scanGroup.Frames
                .Where(x => x != null && x.PreprocessedImage != null)
                .Select(x => x.PreprocessedImage)
                .ToList();
            var first = images.FirstOrDefault();
            var width = first == null ? 1 : first.Width;
            var height = images.Count == 0 ? 1 : images.Sum(x => x.Height);
            var image = new GeneratedVisionImage(
                prefix + "-" + SafeId(scanGroup.ScanGroupId),
                width,
                height,
                string.IsNullOrWhiteSpace(pixelFormat) && first != null ? first.PixelFormat : pixelFormat,
                null,
                imageKind);

            image.Metadata[FlowMetadataKeys.Algorithm] = algorithm;
            image.Metadata[FlowMetadataKeys.ScanGroupId] = scanGroup.ScanGroupId;
            image.Metadata[FlowMetadataKeys.SourceFrameCount] = scanGroup.Frames.Count;
            image.Metadata[FlowMetadataKeys.FrameIndexes] = string.Join(",", scanGroup.Frames.Select(x => x.FrameIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
            return image;
        }
    }

    public static class Final3D2DFusionNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = Final3D2DFusionNodeFactory.TypeName,
                DisplayName = "Final 3D/2D Fusion",
                Category = "Algorithm",
                Version = "1.0.0",
                Description = "Creates fake final 3D and 2D images from a completed scan group.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes missing or invalid scan groups.")
                },
                Settings =
                {
                    CreateStringSetting("ScanGroupResultBinding", "Scan Group Binding", null, false, "Optional binding expression used when ScanGroupResult is not bound directly."),
                    AdapterNodeDescriptors.QueueUseSetting(),
                    AdapterNodeDescriptors.QueueNameSetting(FlowQueueNames.Fusion),
                    AdapterNodeDescriptors.QueueCapacitySetting(),
                    AdapterNodeDescriptors.QueueMaxDegreeSetting(),
                    AdapterNodeDescriptors.QueueFullModeSetting(),
                    AdapterNodeDescriptors.QueueWaitForCompletionSetting()
                },
                Outputs =
                {
                    CreateOutput("Final3DImage", "Final 3D Image", "IVisionImage", "Fake final 3D image."),
                    CreateOutput("Final2DImage", "Final 2D Image", "IVisionImage", "Fake final 2D image."),
                    CreateOutput("HeightMap", "Height Map", "IVisionImage", "Final height map image."),
                    CreateOutput("TextureImage", "Texture Image", "IVisionImage", "Final texture image."),
                    CreateOutput("ConfidenceMap", "Confidence Map", "IVisionImage", "Final confidence map image."),
                    CreateOutput("ScanGroup", "Scan Group", "ScanGroupResult", "Input scan group."),
                    CreateOutput("ScanGroupResult", "Scan Group Result", "ScanGroupResult", "Alias for ScanGroup."),
                    CreateOutput("ScanGroupId", "Scan Group", "String", "Scan group id."),
                    CreateOutput("SourceFrameCount", "Source Frames", "Int32", "Number of fused source frames."),
                    CreateOutput("Queued", "Queued", "Boolean", "True when fusion was queued without waiting."),
                    CreateOutput("QueueCompleted", "Queue Completed", "Boolean", "True when queued fusion completed before node output.")
                }
            };
        }
    }
}
