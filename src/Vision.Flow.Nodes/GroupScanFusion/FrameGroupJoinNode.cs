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
    // 图像组汇合节点将多点位拍照结果收集为有序采集组。
    public sealed class FrameGroupJoinNodeConfig
    {
        public FrameGroupJoinNodeConfig()
        {
            ExpectedShotCount = 2;
            TimeoutMs = 0;
            DuplicatePolicy = "Error";
            FirstShotIndex = 0;
        }

        public string CaptureGroupId { get; set; }

        public string CaptureGroupIdBinding { get; set; }

        public string ShotIndexBinding { get; set; }

        public string FrameBinding { get; set; }

        public string ImageBinding { get; set; }

        public string FrameIdBinding { get; set; }

        public int ExpectedShotCount { get; set; }

        public int TimeoutMs { get; set; }

        public string DuplicatePolicy { get; set; }

        public bool RequireContinuousShotIndex { get; set; }

        public int FirstShotIndex { get; set; }
    }

    public sealed class FrameGroupJoinNodeFactory : BaseNodeFactory<FrameGroupJoinNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.GroupFrameJoin;

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
                CaptureGroupId = GetStringSetting(definition, "CaptureGroupId", null),
                CaptureGroupIdBinding = GetStringSetting(definition, "CaptureGroupIdBinding", null),
                ShotIndexBinding = GetStringSetting(definition, "ShotIndexBinding", null),
                FrameBinding = GetStringSetting(definition, "FrameBinding", null),
                ImageBinding = GetStringSetting(definition, "ImageBinding", null),
                FrameIdBinding = GetStringSetting(definition, "FrameIdBinding", null),
                ExpectedShotCount = GetInt32Setting(definition, "ExpectedShotCount", 2),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 0),
                DuplicatePolicy = GetStringSetting(definition, "DuplicatePolicy", "Error"),
                RequireContinuousShotIndex = Convert.ToBoolean(GetSetting(definition, "RequireContinuousShotIndex", false), CultureInfo.InvariantCulture),
                FirstShotIndex = GetInt32Setting(definition, "FirstShotIndex", 0)
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

            var captureGroupId = ResolveCaptureGroupId(context, _config.CaptureGroupId, _config.CaptureGroupIdBinding);
            if (string.IsNullOrWhiteSpace(captureGroupId))
            {
                return Task.FromResult(NodeExecutionResult.Failure("CaptureGroupId is required."));
            }

            int shotIndex;
            if (!TryResolveInt32(context, "ShotIndex", _config.ShotIndexBinding, out shotIndex))
            {
                return Task.FromResult(NodeExecutionResult.Failure("ShotIndex is required."));
            }

            var item = CreateFrameGroupItem(context, _config, captureGroupId, shotIndex);
            if (item.Image == null && item.Frame == null)
            {
                return Task.FromResult(NodeExecutionResult.Failure("Frame or Image input is required."));
            }

            var duplicatePolicy = ResolveDuplicatePolicy(ResolveString(context, "DuplicatePolicy", _config.DuplicatePolicy));
            var requireContinuous = ResolveBoolean(context, "RequireContinuousShotIndex", _config.RequireContinuousShotIndex);
            var firstShotIndex = ResolveInt32(context, "FirstShotIndex", _config.FirstShotIndex);

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
                    if (duplicatePolicy == DuplicateItemPolicy.Error)
                    {
                        return Task.FromResult(NodeExecutionResult.Failure(
                            "Duplicate ShotIndex detected for CaptureGroupId '" + captureGroupId + "': " + shotIndex));
                    }

                    if (duplicatePolicy == DuplicateItemPolicy.Ignore)
                    {
                        return Task.FromResult(CreateWaitingResult(captureGroupId, bucket.Items.Count, expectedShotCount, timeoutMs, true));
                    }
                }

                bucket.ExpectedCount = expectedShotCount;
                bucket.TimeoutMs = timeoutMs;
                bucket.Items[shotIndex] = item;

                if (bucket.Items.Count < expectedShotCount)
                {
                    return Task.FromResult(CreateWaitingResult(captureGroupId, bucket.Items.Count, expectedShotCount, timeoutMs, false));
                }

                if (requireContinuous && !HasContinuousIndexes(bucket.Items.Keys, expectedShotCount, firstShotIndex))
                {
                    _buckets.Remove(captureGroupId);
                    return Task.FromResult(NodeExecutionResult.Failure(
                        "ShotIndex sequence must be continuous from " + firstShotIndex.ToString(CultureInfo.InvariantCulture) +
                        " for CaptureGroupId '" + captureGroupId + "'."));
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

        private static NodeExecutionResult CreateWaitingResult(
            string captureGroupId,
            int currentCount,
            int expectedCount,
            int timeoutMs,
            bool duplicateIgnored)
        {
            return NodeExecutionResult.Success(
                "Waiting",
                new Dictionary<string, object>
                {
                    { "CaptureGroupId", captureGroupId },
                    { "CurrentShotCount", currentCount },
                    { "ExpectedShotCount", expectedCount },
                    { "TimeoutMs", timeoutMs },
                    { "DuplicateIgnored", duplicateIgnored }
                });
        }

        private static FrameGroupItem CreateFrameGroupItem(
            FlowExecutionContext context,
            FrameGroupJoinNodeConfig config,
            string captureGroupId,
            int shotIndex)
        {
            var frame = ResolveFrame(context, config.FrameBinding);
            var image = frame == null ? ResolveImage(context, config.ImageBinding) : frame.Image;
            var frameId = ResolveStringValue(context, "FrameId", context.Token.FrameId, config.FrameIdBinding);
            var item = new FrameGroupItem
            {
                CaptureGroupId = captureGroupId,
                ShotIndex = shotIndex,
                Frame = frame,
                Image = image,
                FrameId = frame == null ? frameId : frame.FrameId,
                GrabTime = frame == null ? DateTime.UtcNow : frame.GrabTime
            };

            CopyTokenMetadata(context.Token, item.Metadata);
            if (frame != null)
            {
                CopyDictionary(frame.Metadata, item.Metadata);
            }

            item.Metadata[FlowMetadataKeys.CaptureGroupId] = captureGroupId;
            item.Metadata[FlowMetadataKeys.ShotIndex] = shotIndex;
            item.Metadata[FlowMetadataKeys.FrameId] = item.FrameId;
            return item;
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
                    CreateStringSetting("CaptureGroupId", "Capture Group", null, false, "Optional capture group id. Token CaptureGroupId is used when empty."),
                    CreateStringSetting("CaptureGroupIdBinding", "Capture Group Binding", null, false, "Optional binding expression for CaptureGroupId."),
                    CreateStringSetting("ShotIndexBinding", "Shot Index Binding", null, false, "Optional binding expression for ShotIndex."),
                    CreateStringSetting("FrameBinding", "Frame Binding", null, false, "Optional binding expression for Frame."),
                    CreateStringSetting("ImageBinding", "Image Binding", null, false, "Optional binding expression for Image."),
                    CreateStringSetting("FrameIdBinding", "Frame Id Binding", null, false, "Optional binding expression for FrameId."),
                    CreateIntSetting("ExpectedShotCount", "Expected Shots", 2, true, "Number of shots required to complete a group."),
                    CreateIntSetting("TimeoutMs", "Timeout (ms)", 0, false, "Reserved group timeout. Zero disables timeout handling."),
                    CreateStringSetting("DuplicatePolicy", "Duplicate Policy", "Error", false, "Duplicate ShotIndex policy: Error, Ignore, or Replace."),
                    CreateBoolSetting("RequireContinuousShotIndex", "Require Continuous Shots", false, false, "When true, completed ShotIndex values must be continuous."),
                    CreateIntSetting("FirstShotIndex", "First Shot Index", 0, false, "Expected first ShotIndex when continuous validation is enabled.")
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

            result.Metadata[FlowMetadataKeys.CaptureGroupId] = CaptureGroupId;
            result.Metadata[FlowMetadataKeys.ExpectedShotCount] = ExpectedCount;
            result.Metadata[FlowMetadataKeys.ActualShotCount] = frames.Count;
            result.Metadata[FlowMetadataKeys.ShotIndexes] = string.Join(",", frames.Select(x => x.ShotIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
            return result;
        }
    }
}
