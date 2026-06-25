using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Vision.Flow.Core
{
    // Node-specific rules validate settings that depend on known industrial vision node semantics.
    public sealed partial class FlowValidator
    {
        private static void ValidateNodeSpecificRules(
            IList<NodeDefinition> nodes,
            FlowValidationResult result)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Type))
                {
                    continue;
                }

                var fieldPrefix = "Nodes[" + index + "].Settings.";
                if (string.Equals(node.Type, "camera.image_callback", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCameraImageCallbackNode(node, fieldPrefix, result);
                }

                if (string.Equals(node.Type, "recipe.run", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, "image.save", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, "database.save", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, "frame.preprocess", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, "fusion.final_3d_2d", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateQueueSettings(node, fieldPrefix, result);
                }

                if (string.Equals(node.Type, "group.frame_join", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateFrameGroupJoinNode(node, fieldPrefix, result);
                }

                if (string.Equals(node.Type, "scan.group_join", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateScanGroupJoinNode(node, fieldPrefix, result);
                }
            }
        }

        private static void ValidateCameraImageCallbackNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            var callbackMode = GetSettingString(node, "CallbackMode", "WaitNextFrame");
            if (!IsOneOf(callbackMode, "WaitNextFrame", "StreamFrames"))
            {
                result.AddError("CameraCallbackModeInvalid", "CallbackMode must be WaitNextFrame or StreamFrames.", nodeId: node.Id, field: fieldPrefix + "CallbackMode");
            }

            var matchMode = GetSettingString(node, "MatchMode", "TriggerId");
            if (!IsOneOf(matchMode, "TriggerId", "Any", "ScanGroupId", "TimeWindow"))
            {
                result.AddError("CameraMatchModeInvalid", "MatchMode must be TriggerId, Any, ScanGroupId, or TimeWindow.", nodeId: node.Id, field: fieldPrefix + "MatchMode");
            }

            ValidateNonNegativeInt(node, "TimeoutMs", 1000, fieldPrefix, result);
            if (string.Equals(callbackMode, "StreamFrames", StringComparison.OrdinalIgnoreCase))
            {
                var streamOutputMode = GetSettingString(node, "StreamOutputMode", "Batch");
                if (!IsOneOf(streamOutputMode, "Batch", "PerFrame"))
                {
                    result.AddError("CameraStreamOutputModeInvalid", "StreamOutputMode must be Batch or PerFrame.", nodeId: node.Id, field: fieldPrefix + "StreamOutputMode");
                }

                if (string.Equals(streamOutputMode, "PerFrame", StringComparison.OrdinalIgnoreCase))
                {
                    ValidateNonNegativeInt(node, "ExpectedFrameCount", 1, fieldPrefix, result);
                    ValidateNonNegativeInt(node, "StartFrameIndex", 0, fieldPrefix, result);
                }
                else
                {
                    ValidatePositiveInt(node, "ExpectedFrameCount", 1, fieldPrefix, result);
                }

                ValidateNonNegativeInt(node, "FrameTimeoutMs", 1000, fieldPrefix, result);
            }
        }

        private static void ValidateQueueSettings(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            bool useQueue;
            var hasUseQueue = TryGetSettingBoolean(node, "UseQueue", out useQueue, result, fieldPrefix + "UseQueue");
            var validateQueue = hasUseQueue && useQueue;
            validateQueue = validateQueue || HasSetting(node, "QueueName") || HasSetting(node, "QueueCapacity") ||
                HasSetting(node, "QueueMaxDegreeOfParallelism") || HasSetting(node, "QueueFullMode") ||
                HasSetting(node, "WaitForCompletion");

            if (!validateQueue)
            {
                return;
            }

            if (useQueue && string.IsNullOrWhiteSpace(GetSettingString(node, "QueueName", "default")))
            {
                result.AddError("QueueNameMissing", "QueueName is required when UseQueue is true.", nodeId: node.Id, field: fieldPrefix + "QueueName");
            }

            ValidatePositiveInt(node, "QueueCapacity", 16, fieldPrefix, result);
            ValidatePositiveInt(node, "QueueMaxDegreeOfParallelism", 1, fieldPrefix, result);

            var fullMode = GetSettingString(node, "QueueFullMode", "Wait");
            if (!IsOneOf(fullMode, "Wait", "Reject", "Drop", "StopFlow", "NotifyOnly"))
            {
                result.AddError("QueueFullModeInvalid", "QueueFullMode must be Wait, Reject, Drop, StopFlow, or NotifyOnly.", nodeId: node.Id, field: fieldPrefix + "QueueFullMode");
            }

            bool waitForCompletion;
            if (HasSetting(node, "WaitForCompletion"))
            {
                TryGetSettingBoolean(node, "WaitForCompletion", out waitForCompletion, result, fieldPrefix + "WaitForCompletion");
            }
        }

        private static void ValidateFrameGroupJoinNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            ValidatePositiveInt(node, "ExpectedShotCount", 2, fieldPrefix, result);
            ValidateNonNegativeInt(node, "TimeoutMs", 0, fieldPrefix, result);
            ValidateDuplicatePolicy(node, fieldPrefix, result);

            bool requireContinuous;
            if (TryGetSettingBoolean(node, "RequireContinuousShotIndex", out requireContinuous, result, fieldPrefix + "RequireContinuousShotIndex") &&
                requireContinuous)
            {
                ValidateNonNegativeInt(node, "FirstShotIndex", 0, fieldPrefix, result);
            }
        }

        private static void ValidateScanGroupJoinNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            ValidatePositiveInt(node, "ExpectedFrameCount", 2, fieldPrefix, result);
            ValidateNonNegativeInt(node, "TimeoutMs", 0, fieldPrefix, result);
            ValidateDuplicatePolicy(node, fieldPrefix, result);

            bool requireContinuous;
            if (TryGetSettingBoolean(node, "RequireContinuousFrameIndex", out requireContinuous, result, fieldPrefix + "RequireContinuousFrameIndex") &&
                requireContinuous)
            {
                ValidateNonNegativeInt(node, "FirstFrameIndex", 0, fieldPrefix, result);
            }
        }

        private static void ValidateDuplicatePolicy(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            var duplicatePolicy = GetSettingString(node, "DuplicatePolicy", "Error");
            if (!IsOneOf(duplicatePolicy, "Error", "Ignore", "Replace"))
            {
                result.AddError("DuplicatePolicyInvalid", "DuplicatePolicy must be Error, Ignore, or Replace.", nodeId: node.Id, field: fieldPrefix + "DuplicatePolicy");
            }
        }
    }
}
