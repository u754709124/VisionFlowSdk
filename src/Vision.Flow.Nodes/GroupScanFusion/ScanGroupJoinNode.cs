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
    // 扫描组汇合节点将预处理帧收集为有序扫描组。
    public sealed class ScanGroupJoinNodeConfig
    {
        public ScanGroupJoinNodeConfig()
        {
            ExpectedFrameCount = 2;
            TimeoutMs = 0;
            DuplicatePolicy = "Error";
            FirstFrameIndex = 0;
        }

        public string PreprocessResultBinding { get; set; }

        public int ExpectedFrameCount { get; set; }

        public int TimeoutMs { get; set; }

        public string DuplicatePolicy { get; set; }

        public bool RequireContinuousFrameIndex { get; set; }

        public int FirstFrameIndex { get; set; }
    }

    public sealed class ScanGroupJoinNodeFactory : BaseNodeFactory<ScanGroupJoinNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.ScanGroupJoin;

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
                PreprocessResultBinding = GetStringSetting(definition, "PreprocessResultBinding", null),
                ExpectedFrameCount = GetInt32Setting(definition, "ExpectedFrameCount", 2),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 0),
                DuplicatePolicy = GetStringSetting(definition, "DuplicatePolicy", "Error"),
                RequireContinuousFrameIndex = Convert.ToBoolean(GetSetting(definition, "RequireContinuousFrameIndex", false), CultureInfo.InvariantCulture),
                FirstFrameIndex = GetInt32Setting(definition, "FirstFrameIndex", 0)
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

            var preprocessResult = ResolvePreprocessResult(context, _config.PreprocessResultBinding);
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

            var duplicatePolicy = ResolveDuplicatePolicy(ResolveString(context, "DuplicatePolicy", _config.DuplicatePolicy));
            var requireContinuous = ResolveBoolean(context, "RequireContinuousFrameIndex", _config.RequireContinuousFrameIndex);
            var firstFrameIndex = ResolveInt32(context, "FirstFrameIndex", _config.FirstFrameIndex);

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
                    if (duplicatePolicy == DuplicateItemPolicy.Error)
                    {
                        return Task.FromResult(NodeExecutionResult.Failure(
                            "Duplicate FrameIndex detected for ScanGroupId '" + preprocessResult.ScanGroupId + "': " + preprocessResult.FrameIndex));
                    }

                    if (duplicatePolicy == DuplicateItemPolicy.Ignore)
                    {
                        return Task.FromResult(CreateWaitingResult(preprocessResult.ScanGroupId, bucket.Items.Count, expectedFrameCount, timeoutMs, true));
                    }
                }

                bucket.ExpectedCount = expectedFrameCount;
                bucket.TimeoutMs = timeoutMs;
                bucket.Items[preprocessResult.FrameIndex] = preprocessResult;

                if (bucket.Items.Count < expectedFrameCount)
                {
                    return Task.FromResult(CreateWaitingResult(preprocessResult.ScanGroupId, bucket.Items.Count, expectedFrameCount, timeoutMs, false));
                }

                if (requireContinuous && !HasContinuousIndexes(bucket.Items.Keys, expectedFrameCount, firstFrameIndex))
                {
                    _buckets.Remove(preprocessResult.ScanGroupId);
                    return Task.FromResult(NodeExecutionResult.Failure(
                        "FrameIndex sequence must be continuous from " + firstFrameIndex.ToString(CultureInfo.InvariantCulture) +
                        " for ScanGroupId '" + preprocessResult.ScanGroupId + "'."));
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

        private static NodeExecutionResult CreateWaitingResult(
            string scanGroupId,
            int currentCount,
            int expectedCount,
            int timeoutMs,
            bool duplicateIgnored)
        {
            return NodeExecutionResult.Success(
                "Waiting",
                new Dictionary<string, object>
                {
                    { "ScanGroupId", scanGroupId },
                    { "CurrentFrameCount", currentCount },
                    { "ExpectedFrameCount", expectedCount },
                    { "TimeoutMs", timeoutMs },
                    { "DuplicateIgnored", duplicateIgnored }
                });
        }

        private static FramePreprocessResult ResolvePreprocessResult(FlowExecutionContext context, string bindingExpression)
        {
            var value = ResolveConfiguredValue(context, "PreprocessResult", bindingExpression);
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
                    CreateStringSetting("PreprocessResultBinding", "Preprocess Binding", null, false, "Optional binding expression used when PreprocessResult is not bound directly."),
                    CreateIntSetting("ExpectedFrameCount", "Expected Frames", 2, true, "Number of preprocessed frames required to complete a group."),
                    CreateIntSetting("TimeoutMs", "Timeout (ms)", 0, false, "Reserved group timeout. Zero disables timeout handling."),
                    CreateStringSetting("DuplicatePolicy", "Duplicate Policy", "Error", false, "Duplicate FrameIndex policy: Error, Ignore, or Replace."),
                    CreateBoolSetting("RequireContinuousFrameIndex", "Require Continuous Frames", false, false, "When true, completed FrameIndex values must be continuous."),
                    CreateIntSetting("FirstFrameIndex", "First Frame Index", 0, false, "Expected first FrameIndex when continuous validation is enabled.")
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

            result.Metadata[FlowMetadataKeys.ScanGroupId] = ScanGroupId;
            result.Metadata[FlowMetadataKeys.ExpectedFrameCount] = ExpectedCount;
            result.Metadata[FlowMetadataKeys.ActualFrameCount] = frames.Count;
            result.Metadata[FlowMetadataKeys.FrameIndexes] = string.Join(",", frames.Select(x => x.FrameIndex.ToString(CultureInfo.InvariantCulture)).ToArray());
            return result;
        }
    }
}
