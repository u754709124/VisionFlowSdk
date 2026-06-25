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
    public sealed class GeneratedVisionImage : IVisionImage
    {
        public GeneratedVisionImage(string imageId, int width, int height, string pixelFormat, byte[] data)
        {
            ImageId = string.IsNullOrWhiteSpace(imageId) ? Guid.NewGuid().ToString("N") : imageId;
            Width = width <= 0 ? 1 : width;
            Height = height <= 0 ? 1 : height;
            PixelFormat = string.IsNullOrWhiteSpace(pixelFormat) ? "Mono8" : pixelFormat;
            CreatedUtc = DateTime.UtcNow;
            Data = data ?? new byte[0];
            Metadata = new Dictionary<string, object>();
        }

        public string ImageId { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public string PixelFormat { get; private set; }

        public DateTime CreatedUtc { get; private set; }

        public byte[] Data { get; private set; }

        public object NativeImage
        {
            get { return null; }
        }

        public bool IsDisposed { get; private set; }

        public IDictionary<string, object> Metadata { get; private set; }

        public IVisionImage CloneReference()
        {
            var clone = new GeneratedVisionImage(ImageId, Width, Height, PixelFormat, Data)
            {
                CreatedUtc = CreatedUtc
            };
            CopyMetadata(Metadata, clone.Metadata);
            return clone;
        }

        public bool TryGetBytes(out byte[] data)
        {
            data = null;
            if (IsDisposed || Data == null)
            {
                return false;
            }

            data = new byte[Data.Length];
            if (Data.Length > 0)
            {
                Buffer.BlockCopy(Data, 0, data, 0, Data.Length);
            }

            return true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            Data = new byte[0];
        }

        private static void CopyMetadata(IDictionary<string, object> source, IDictionary<string, object> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (var item in source)
            {
                target[item.Key] = item.Value;
            }
        }
    }

    public sealed class FrameGroupItem
    {
        public FrameGroupItem()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string CaptureGroupId { get; set; }

        public int ShotIndex { get; set; }

        public CameraFrameData Frame { get; set; }

        public IVisionImage Image { get; set; }

        public string FrameId { get; set; }

        public DateTime GrabTime { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class FrameGroupResult
    {
        public FrameGroupResult()
        {
            Frames = new List<FrameGroupItem>();
            Metadata = new Dictionary<string, object>();
        }

        public string CaptureGroupId { get; set; }

        public int ExpectedShotCount { get; set; }

        public int ActualShotCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime CompletedAtUtc { get; set; }

        public IList<FrameGroupItem> Frames { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class FramePreprocessResult
    {
        public FramePreprocessResult()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string ScanGroupId { get; set; }

        public int FrameIndex { get; set; }

        public CameraFrameData SourceFrame { get; set; }

        public IVisionImage SourceImage { get; set; }

        public IVisionImage PreprocessedImage { get; set; }

        public string FrameId { get; set; }

        public DateTime ProcessedAtUtc { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class ScanGroupResult
    {
        public ScanGroupResult()
        {
            Frames = new List<FramePreprocessResult>();
            Metadata = new Dictionary<string, object>();
        }

        public string ScanGroupId { get; set; }

        public int ExpectedFrameCount { get; set; }

        public int ActualFrameCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime CompletedAtUtc { get; set; }

        public IList<FramePreprocessResult> Frames { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class FrameGroupJoinNodeConfig
    {
        public FrameGroupJoinNodeConfig()
        {
            ExpectedShotCount = 2;
            TimeoutMs = 0;
        }

        public int ExpectedShotCount { get; set; }

        public int TimeoutMs { get; set; }
    }

    public sealed class FrameGroupJoinNodeFactory : BaseNodeFactory<FrameGroupJoinNodeConfig>
    {
        public const string TypeName = "group.frame_join";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return FrameGroupJoinNodeDescriptor.Create(); }
        }

        protected override FrameGroupJoinNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new FrameGroupJoinNodeConfig
            {
                ExpectedShotCount = GetInt32Setting(definition, "ExpectedShotCount", 2),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 0)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, FrameGroupJoinNodeConfig config)
        {
            return new FrameGroupJoinNode(config);
        }
    }

    public sealed class FrameGroupJoinNode : IFlowNode
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, FrameGroupBucket> _buckets;
        private readonly FrameGroupJoinNodeConfig _config;

        public FrameGroupJoinNode(FrameGroupJoinNodeConfig config)
        {
            _config = config ?? new FrameGroupJoinNodeConfig();
            _buckets = new Dictionary<string, FrameGroupBucket>(StringComparer.OrdinalIgnoreCase);
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedShotCount = ResolveInt32(context, "ExpectedShotCount", _config.ExpectedShotCount);
            if (expectedShotCount <= 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("ExpectedShotCount must be greater than zero."));
            }

            var timeoutMs = ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero."));
            }

            var captureGroupId = ResolveCaptureGroupId(context);
            if (string.IsNullOrWhiteSpace(captureGroupId))
            {
                return Task.FromResult(NodeExecutionResult.Failure("CaptureGroupId is required."));
            }

            int shotIndex;
            if (!TryResolveInt32(context, "ShotIndex", out shotIndex))
            {
                return Task.FromResult(NodeExecutionResult.Failure("ShotIndex is required."));
            }

            var item = CreateFrameGroupItem(context, captureGroupId, shotIndex);
            if (item.Image == null && item.Frame == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("Frame or Image input is required."));
            }

            lock (_gate)
            {
                FrameGroupBucket bucket;
                if (!_buckets.TryGetValue(captureGroupId, out bucket))
                {
                    bucket = new FrameGroupBucket(captureGroupId, expectedShotCount, timeoutMs);
                    _buckets[captureGroupId] = bucket;
                }

                if (bucket.Items.ContainsKey(shotIndex))
                {
                    return Task.FromResult(NodeExecutionResult.Failure(
                        "Duplicate ShotIndex detected for CaptureGroupId '" + captureGroupId + "': " + shotIndex));
                }

                bucket.ExpectedCount = expectedShotCount;
                bucket.TimeoutMs = timeoutMs;
                bucket.Items[shotIndex] = item;

                if (bucket.Items.Count < expectedShotCount)
                {
                    return Task.FromResult(NodeExecutionResult.Success(
                        "Waiting",
                        new Dictionary<string, object>
                        {
                            { "CaptureGroupId", captureGroupId },
                            { "CurrentShotCount", bucket.Items.Count },
                            { "ExpectedShotCount", expectedShotCount },
                            { "TimeoutMs", timeoutMs }
                        }));
                }

                _buckets.Remove(captureGroupId);
                var result = bucket.CreateResult();
                return Task.FromResult(NodeExecutionResult.Success(
                    "Next",
                    new Dictionary<string, object>
                    {
                        { "FrameGroup", result },
                        { "FrameGroupResult", result },
                        { "GroupToken", captureGroupId },
                        { "CaptureGroupId", captureGroupId },
                        { "FrameCount", result.ActualShotCount },
                        { "ExpectedShotCount", result.ExpectedShotCount }
                    }));
            }
        }

        private static FrameGroupItem CreateFrameGroupItem(FlowExecutionContext context, string captureGroupId, int shotIndex)
        {
            var frame = ResolveFrame(context);
            var image = frame == null ? ResolveImage(context) : frame.Image;
            var item = new FrameGroupItem
            {
                CaptureGroupId = captureGroupId,
                ShotIndex = shotIndex,
                Frame = frame,
                Image = image,
                FrameId = frame == null ? ResolveString(context, "FrameId", context.Token.FrameId) : frame.FrameId,
                GrabTime = frame == null ? DateTime.UtcNow : frame.GrabTime
            };

            CopyTokenMetadata(context.Token, item.Metadata);
            if (frame != null)
            {
                CopyDictionary(frame.Metadata, item.Metadata);
            }

            item.Metadata["CaptureGroupId"] = captureGroupId;
            item.Metadata["ShotIndex"] = shotIndex;
            item.Metadata["FrameId"] = item.FrameId;
            return item;
        }

        private static string ResolveCaptureGroupId(FlowExecutionContext context)
        {
            var value = ResolveString(context, "CaptureGroupId", null);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            object tokenValue;
            if (context.Token.TryGet("CaptureGroupId", out tokenValue))
            {
                return tokenValue == null ? null : Convert.ToString(tokenValue, CultureInfo.InvariantCulture);
            }

            return context.Token.CaptureGroupId;
        }
    }

    public static class FrameGroupJoinNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = FrameGroupJoinNodeFactory.TypeName,
                DisplayName = "Frame Group Join",
                Category = "Group",
                Version = "1.0.0",
                Description = "Collects frames by CaptureGroupId and emits a sorted frame group.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    new NodePortDescriptor
                    {
                        Name = "Waiting",
                        DisplayName = "Waiting",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes while the group is not complete."
                    },
                    AdapterNodeDescriptors.TimeoutOut("Reserved for future timed group completion."),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid input or duplicate ShotIndex.")
                },
                Settings =
                {
                    CreateIntSetting("ExpectedShotCount", "Expected Shots", 2, true, "Number of shots required to complete a group."),
                    CreateIntSetting("TimeoutMs", "Timeout (ms)", 0, false, "Reserved group timeout. Zero disables timeout handling.")
                },
                Outputs =
                {
                    CreateOutput("FrameGroup", "Frame Group", "FrameGroupResult", "Completed frame group sorted by ShotIndex."),
                    CreateOutput("FrameGroupResult", "Frame Group Result", "FrameGroupResult", "Alias for FrameGroup."),
                    CreateOutput("GroupToken", "Group Token", "String", "Completed CaptureGroupId."),
                    CreateOutput("CaptureGroupId", "Capture Group", "String", "Completed capture group id."),
                    CreateOutput("FrameCount", "Frame Count", "Int32", "Number of frames in the group."),
                    CreateOutput("ExpectedShotCount", "Expected Shots", "Int32", "Expected shot count.")
                }
            };
        }
    }

    public sealed class StitchNodeConfig
    {
    }

    public sealed class StitchNodeFactory : BaseNodeFactory<StitchNodeConfig>
    {
        public const string TypeName = "image.stitch";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return StitchNodeDescriptor.Create(); }
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, StitchNodeConfig config)
        {
            return new StitchNode(config);
        }
    }

    public sealed class StitchNode : IFlowNode
    {
        public StitchNode(StitchNodeConfig config)
        {
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frameGroup = ResolveFrameGroup(context);
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

        private static FrameGroupResult ResolveFrameGroup(FlowExecutionContext context)
        {
            var value = context.GetInputValue("FrameGroup");
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
            var image = new GeneratedVisionImage(imageId, width, height, pixelFormat, null);

            image.Metadata["Algorithm"] = "FakeStitch";
            image.Metadata["CaptureGroupId"] = frameGroup.CaptureGroupId;
            image.Metadata["SourceFrameCount"] = frameGroup.Frames.Count;
            image.Metadata["ShotIndexes"] = string.Join(",", frameGroup.Frames.Select(x => x.ShotIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
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

    public sealed class FramePreprocessNodeConfig
    {
        public FramePreprocessNodeConfig()
        {
            FrameIndex = -1;
        }

        public string ScanGroupId { get; set; }

        public int FrameIndex { get; set; }
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
                FrameIndex = GetInt32Setting(definition, "FrameIndex", -1)
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
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanGroupId = ResolveScanGroupId(context, _config.ScanGroupId);
            if (string.IsNullOrWhiteSpace(scanGroupId))
            {
                return Task.FromResult(NodeExecutionResult.Failure("ScanGroupId is required."));
            }

            int frameIndex;
            if (!TryResolveInt32(context, "FrameIndex", out frameIndex))
            {
                frameIndex = _config.FrameIndex;
            }

            if (frameIndex < 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("FrameIndex must be greater than or equal to zero."));
            }

            var frame = ResolveFrame(context);
            var sourceImage = frame == null ? ResolveImage(context) : frame.Image;
            if (sourceImage == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("Frame or Image input is required."));
            }

            var result = CreatePreprocessResult(context, scanGroupId, frameIndex, frame, sourceImage);
            return Task.FromResult(NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "PreprocessResult", result },
                    { "FramePreprocessResult", result },
                    { "PreprocessedImage", result.PreprocessedImage },
                    { "ScanGroupId", scanGroupId },
                    { "FrameIndex", frameIndex },
                    { "FrameId", result.FrameId }
                }));
        }

        private static FramePreprocessResult CreatePreprocessResult(
            FlowExecutionContext context,
            string scanGroupId,
            int frameIndex,
            CameraFrameData frame,
            IVisionImage sourceImage)
        {
            var sourceImageId = sourceImage == null ? null : sourceImage.ImageId;
            var frameId = frame == null ? ResolveString(context, "FrameId", context.Token.FrameId) : frame.FrameId;
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
                null);
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
                    CreateIntSetting("FrameIndex", "Frame Index", -1, false, "Frame index. Values less than zero require an input or token value.")
                },
                Outputs =
                {
                    CreateOutput("PreprocessResult", "Preprocess Result", "FramePreprocessResult", "Frame preprocessing result."),
                    CreateOutput("FramePreprocessResult", "Frame Preprocess Result", "FramePreprocessResult", "Alias for PreprocessResult."),
                    CreateOutput("PreprocessedImage", "Preprocessed Image", "IVisionImage", "Fake preprocessed image."),
                    CreateOutput("ScanGroupId", "Scan Group", "String", "Scan group id."),
                    CreateOutput("FrameIndex", "Frame Index", "Int32", "Frame index."),
                    CreateOutput("FrameId", "Frame Id", "String", "Source frame id.")
                }
            };
        }
    }

    public sealed class ScanGroupJoinNodeConfig
    {
        public ScanGroupJoinNodeConfig()
        {
            ExpectedFrameCount = 2;
            TimeoutMs = 0;
        }

        public int ExpectedFrameCount { get; set; }

        public int TimeoutMs { get; set; }
    }

    public sealed class ScanGroupJoinNodeFactory : BaseNodeFactory<ScanGroupJoinNodeConfig>
    {
        public const string TypeName = "scan.group_join";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return ScanGroupJoinNodeDescriptor.Create(); }
        }

        protected override ScanGroupJoinNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new ScanGroupJoinNodeConfig
            {
                ExpectedFrameCount = GetInt32Setting(definition, "ExpectedFrameCount", 2),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 0)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, ScanGroupJoinNodeConfig config)
        {
            return new ScanGroupJoinNode(config);
        }
    }

    public sealed class ScanGroupJoinNode : IFlowNode
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, ScanGroupBucket> _buckets;
        private readonly ScanGroupJoinNodeConfig _config;

        public ScanGroupJoinNode(ScanGroupJoinNodeConfig config)
        {
            _config = config ?? new ScanGroupJoinNodeConfig();
            _buckets = new Dictionary<string, ScanGroupBucket>(StringComparer.OrdinalIgnoreCase);
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedFrameCount = ResolveInt32(context, "ExpectedFrameCount", _config.ExpectedFrameCount);
            if (expectedFrameCount <= 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("ExpectedFrameCount must be greater than zero."));
            }

            var timeoutMs = ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero."));
            }

            var preprocessResult = ResolvePreprocessResult(context);
            if (preprocessResult == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("PreprocessResult input is required."));
            }

            if (string.IsNullOrWhiteSpace(preprocessResult.ScanGroupId))
            {
                return Task.FromResult(NodeExecutionResult.Failure("PreprocessResult.ScanGroupId is required."));
            }

            if (preprocessResult.FrameIndex < 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("PreprocessResult.FrameIndex must be greater than or equal to zero."));
            }

            lock (_gate)
            {
                ScanGroupBucket bucket;
                if (!_buckets.TryGetValue(preprocessResult.ScanGroupId, out bucket))
                {
                    bucket = new ScanGroupBucket(preprocessResult.ScanGroupId, expectedFrameCount, timeoutMs);
                    _buckets[preprocessResult.ScanGroupId] = bucket;
                }

                if (bucket.Items.ContainsKey(preprocessResult.FrameIndex))
                {
                    return Task.FromResult(NodeExecutionResult.Failure(
                        "Duplicate FrameIndex detected for ScanGroupId '" + preprocessResult.ScanGroupId + "': " + preprocessResult.FrameIndex));
                }

                bucket.ExpectedCount = expectedFrameCount;
                bucket.TimeoutMs = timeoutMs;
                bucket.Items[preprocessResult.FrameIndex] = preprocessResult;

                if (bucket.Items.Count < expectedFrameCount)
                {
                    return Task.FromResult(NodeExecutionResult.Success(
                        "Waiting",
                        new Dictionary<string, object>
                        {
                            { "ScanGroupId", preprocessResult.ScanGroupId },
                            { "CurrentFrameCount", bucket.Items.Count },
                            { "ExpectedFrameCount", expectedFrameCount },
                            { "TimeoutMs", timeoutMs }
                        }));
                }

                _buckets.Remove(preprocessResult.ScanGroupId);
                var result = bucket.CreateResult();
                return Task.FromResult(NodeExecutionResult.Success(
                    "Next",
                    new Dictionary<string, object>
                    {
                        { "ScanGroup", result },
                        { "ScanGroupResult", result },
                        { "ScanGroupId", result.ScanGroupId },
                        { "FrameCount", result.ActualFrameCount },
                        { "ExpectedFrameCount", result.ExpectedFrameCount }
                    }));
            }
        }

        private static FramePreprocessResult ResolvePreprocessResult(FlowExecutionContext context)
        {
            var value = context.GetInputValue("PreprocessResult");
            if (value == null)
            {
                value = context.GetInputValue("FramePreprocessResult");
            }

            return value as FramePreprocessResult;
        }
    }

    public static class ScanGroupJoinNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = ScanGroupJoinNodeFactory.TypeName,
                DisplayName = "Scan Group Join",
                Category = "Group",
                Version = "1.0.0",
                Description = "Collects preprocessing results by ScanGroupId and emits a sorted scan group.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    new NodePortDescriptor
                    {
                        Name = "Waiting",
                        DisplayName = "Waiting",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes while the scan group is not complete."
                    },
                    AdapterNodeDescriptors.TimeoutOut("Reserved for future timed scan group completion."),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid input or duplicate FrameIndex.")
                },
                Settings =
                {
                    CreateIntSetting("ExpectedFrameCount", "Expected Frames", 2, true, "Number of preprocessed frames required to complete a group."),
                    CreateIntSetting("TimeoutMs", "Timeout (ms)", 0, false, "Reserved group timeout. Zero disables timeout handling.")
                },
                Outputs =
                {
                    CreateOutput("ScanGroup", "Scan Group", "ScanGroupResult", "Completed scan group sorted by FrameIndex."),
                    CreateOutput("ScanGroupResult", "Scan Group Result", "ScanGroupResult", "Alias for ScanGroup."),
                    CreateOutput("ScanGroupId", "Scan Group", "String", "Completed scan group id."),
                    CreateOutput("FrameCount", "Frame Count", "Int32", "Number of frames in the scan group."),
                    CreateOutput("ExpectedFrameCount", "Expected Frames", "Int32", "Expected frame count.")
                }
            };
        }
    }

    public sealed class Final3D2DFusionNodeConfig
    {
    }

    public sealed class Final3D2DFusionNodeFactory : BaseNodeFactory<Final3D2DFusionNodeConfig>
    {
        public const string TypeName = "fusion.final_3d_2d";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return Final3D2DFusionNodeDescriptor.Create(); }
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, Final3D2DFusionNodeConfig config)
        {
            return new Final3D2DFusionNode(config);
        }
    }

    public sealed class Final3D2DFusionNode : IFlowNode
    {
        public Final3D2DFusionNode(Final3D2DFusionNodeConfig config)
        {
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scanGroup = ResolveScanGroup(context);
            if (scanGroup == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("ScanGroupResult input is required."));
            }

            if (scanGroup.Frames == null || scanGroup.Frames.Count == 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("ScanGroupResult must contain at least one frame."));
            }

            var final3D = CreateFusionImage(scanGroup, "fusion3d", "FakeFinal3D", "Float32");
            var final2D = CreateFusionImage(scanGroup, "fusion2d", "FakeFinal2D", null);
            return Task.FromResult(NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "Final3DImage", final3D },
                    { "Final2DImage", final2D },
                    { "ScanGroup", scanGroup },
                    { "ScanGroupResult", scanGroup },
                    { "ScanGroupId", scanGroup.ScanGroupId },
                    { "SourceFrameCount", scanGroup.Frames.Count }
                }));
        }

        private static ScanGroupResult ResolveScanGroup(FlowExecutionContext context)
        {
            var value = context.GetInputValue("ScanGroupResult");
            if (value == null)
            {
                value = context.GetInputValue("ScanGroup");
            }

            return value as ScanGroupResult;
        }

        private static IVisionImage CreateFusionImage(ScanGroupResult scanGroup, string prefix, string algorithm, string pixelFormat)
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
                null);

            image.Metadata["Algorithm"] = algorithm;
            image.Metadata["ScanGroupId"] = scanGroup.ScanGroupId;
            image.Metadata["SourceFrameCount"] = scanGroup.Frames.Count;
            image.Metadata["FrameIndexes"] = string.Join(",", scanGroup.Frames.Select(x => x.FrameIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
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
                Outputs =
                {
                    CreateOutput("Final3DImage", "Final 3D Image", "IVisionImage", "Fake final 3D image."),
                    CreateOutput("Final2DImage", "Final 2D Image", "IVisionImage", "Fake final 2D image."),
                    CreateOutput("ScanGroup", "Scan Group", "ScanGroupResult", "Input scan group."),
                    CreateOutput("ScanGroupResult", "Scan Group Result", "ScanGroupResult", "Alias for ScanGroup."),
                    CreateOutput("ScanGroupId", "Scan Group", "String", "Scan group id."),
                    CreateOutput("SourceFrameCount", "Source Frames", "Int32", "Number of fused source frames.")
                }
            };
        }
    }

    internal sealed class FrameGroupBucket
    {
        public FrameGroupBucket(string captureGroupId, int expectedCount, int timeoutMs)
        {
            CaptureGroupId = captureGroupId;
            ExpectedCount = expectedCount;
            TimeoutMs = timeoutMs;
            CreatedAtUtc = DateTime.UtcNow;
            Items = new Dictionary<int, FrameGroupItem>();
        }

        public string CaptureGroupId { get; private set; }

        public int ExpectedCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; private set; }

        public Dictionary<int, FrameGroupItem> Items { get; private set; }

        public FrameGroupResult CreateResult()
        {
            var frames = Items.OrderBy(x => x.Key).Select(x => x.Value).ToList();
            var result = new FrameGroupResult
            {
                CaptureGroupId = CaptureGroupId,
                ExpectedShotCount = ExpectedCount,
                ActualShotCount = frames.Count,
                TimeoutMs = TimeoutMs,
                CreatedAtUtc = CreatedAtUtc,
                CompletedAtUtc = DateTime.UtcNow,
                Frames = frames
            };

            result.Metadata["CaptureGroupId"] = CaptureGroupId;
            result.Metadata["ExpectedShotCount"] = ExpectedCount;
            result.Metadata["ActualShotCount"] = frames.Count;
            result.Metadata["ShotIndexes"] = string.Join(",", frames.Select(x => x.ShotIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
            return result;
        }
    }

    internal sealed class ScanGroupBucket
    {
        public ScanGroupBucket(string scanGroupId, int expectedCount, int timeoutMs)
        {
            ScanGroupId = scanGroupId;
            ExpectedCount = expectedCount;
            TimeoutMs = timeoutMs;
            CreatedAtUtc = DateTime.UtcNow;
            Items = new Dictionary<int, FramePreprocessResult>();
        }

        public string ScanGroupId { get; private set; }

        public int ExpectedCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; private set; }

        public Dictionary<int, FramePreprocessResult> Items { get; private set; }

        public ScanGroupResult CreateResult()
        {
            var frames = Items.OrderBy(x => x.Key).Select(x => x.Value).ToList();
            var result = new ScanGroupResult
            {
                ScanGroupId = ScanGroupId,
                ExpectedFrameCount = ExpectedCount,
                ActualFrameCount = frames.Count,
                TimeoutMs = TimeoutMs,
                CreatedAtUtc = CreatedAtUtc,
                CompletedAtUtc = DateTime.UtcNow,
                Frames = frames
            };

            result.Metadata["ScanGroupId"] = ScanGroupId;
            result.Metadata["ExpectedFrameCount"] = ExpectedCount;
            result.Metadata["ActualFrameCount"] = frames.Count;
            result.Metadata["FrameIndexes"] = string.Join(",", frames.Select(x => x.FrameIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
            return result;
        }
    }

    internal static class GroupScanFusionNodeHelpers
    {
        internal static NodeSettingDescriptor CreateStringSetting(
        string name,
        string displayName,
        string defaultValue,
        bool isRequired,
        string description)
    {
        return new NodeSettingDescriptor
        {
            Name = name,
            DisplayName = displayName,
            DataType = "String",
            DefaultValue = defaultValue,
            IsRequired = isRequired,
            Description = description
        };
    }

        internal static NodeSettingDescriptor CreateIntSetting(
        string name,
        string displayName,
        int defaultValue,
        bool isRequired,
        string description)
    {
        return new NodeSettingDescriptor
        {
            Name = name,
            DisplayName = displayName,
            DataType = "Int32",
            DefaultValue = defaultValue,
            IsRequired = isRequired,
            Description = description
        };
    }

        internal static NodeOutputDescriptor CreateOutput(string name, string displayName, string dataType, string description)
    {
        return new NodeOutputDescriptor
        {
            Name = name,
            DisplayName = displayName,
            DataType = dataType,
            Description = description
        };
    }

        internal static CameraFrameData ResolveFrame(FlowExecutionContext context)
    {
        var value = context.GetInputValue("Frame");
        if (value == null)
        {
            object tokenValue;
            if (context.Token.TryGet("Frame", out tokenValue))
            {
                value = tokenValue;
            }
        }

        var frame = value as CameraFrameData;
        if (value != null && frame == null)
        {
            throw new InvalidCastException("Frame must be CameraFrameData.");
        }

        return frame;
    }

        internal static IVisionImage ResolveImage(FlowExecutionContext context)
    {
        var value = context.GetInputValue("Image");
        if (value == null)
        {
            object tokenValue;
            if (context.Token.TryGet("Image", out tokenValue))
            {
                value = tokenValue;
            }
        }

        return AdapterNodeHelpers.ResolveVisionImage(value, "Image");
    }

        internal static string ResolveScanGroupId(FlowExecutionContext context, string defaultValue)
    {
        var value = ResolveString(context, "ScanGroupId", defaultValue);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        object tokenValue;
        if (context.Token.TryGet("ScanGroupId", out tokenValue))
        {
            return tokenValue == null ? null : Convert.ToString(tokenValue, CultureInfo.InvariantCulture);
        }

        return context.Token.ScanGroupId;
    }

        internal static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
    {
        var value = context.GetInputValue(name);
        return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

        internal static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
    {
        int value;
        return TryResolveInt32(context, name, out value) ? value : defaultValue;
    }

        internal static bool TryResolveInt32(FlowExecutionContext context, string name, out int value)
    {
        object inputValue = context.GetInputValue(name);
        if (inputValue == null)
        {
            object tokenValue;
            if (context.Token.TryGet(name, out tokenValue))
            {
                inputValue = tokenValue;
            }
        }

        if (inputValue == null)
        {
            value = 0;
            return false;
        }

        value = Convert.ToInt32(inputValue, CultureInfo.InvariantCulture);
        return true;
    }

        internal static void CopyTokenMetadata(FlowToken token, IDictionary<string, object> metadata)
    {
        if (token == null || metadata == null)
        {
            return;
        }

        metadata["TokenId"] = token.TokenId;
        metadata["ProductId"] = token.ProductId;
        metadata["WorkpieceId"] = token.WorkpieceId;
        metadata["PositionId"] = token.PositionId;
        metadata["CaptureGroupId"] = token.CaptureGroupId;
        metadata["ScanGroupId"] = token.ScanGroupId;
        metadata["FrameId"] = token.FrameId;
        CopyDictionary(token.Metadata, metadata);
    }

        internal static void CopyDictionary(IDictionary<string, object> source, IDictionary<string, object> target)
    {
        if (source == null || target == null)
        {
            return;
        }

        foreach (var item in source)
        {
            target[item.Key] = item.Value;
        }
    }

        internal static string SafeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Replace(" ", "_");
    }
    }
}
