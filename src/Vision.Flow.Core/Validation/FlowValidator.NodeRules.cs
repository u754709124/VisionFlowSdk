using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Vision.Flow.Core
{
    // 节点专项规则校验依赖具体工业视觉节点语义的配置。
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
                if (string.Equals(node.Type, FlowNodeTypes.CameraImageCallback, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateCameraImageCallbackNode(node, fieldPrefix, result);
                }

                if (string.Equals(node.Type, FlowNodeTypes.RecipeRun, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, FlowNodeTypes.ImageSave, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, FlowNodeTypes.DatabaseSave, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, FlowNodeTypes.FramePreprocess, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(node.Type, FlowNodeTypes.FusionFinal3D2D, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateQueueSettings(node, fieldPrefix, result);
                }

                if (string.Equals(node.Type, FlowNodeTypes.GroupFrameJoin, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateFrameGroupJoinNode(node, fieldPrefix, result);
                }

                if (string.Equals(node.Type, FlowNodeTypes.ScanGroupJoin, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateScanGroupJoinNode(node, fieldPrefix, result);
                }
            }
        }

        private static void ValidateCameraImageCallbackNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            var callbackMode = GetSettingString(node, FlowSettingNames.CallbackMode, CameraCallbackModes.WaitNextFrame);
            if (!IsOneOf(callbackMode, CameraCallbackModes.WaitNextFrame, CameraCallbackModes.StreamFrames))
            {
                result.AddError(FlowValidationIssueCodes.CameraCallbackModeInvalid, "CallbackMode must be WaitNextFrame or StreamFrames.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.CallbackMode);
            }

            var matchMode = GetSettingString(node, FlowSettingNames.MatchMode, CameraFrameMatchModes.TriggerId);
            if (!IsOneOf(matchMode, "TriggerId", "Any", "ScanGroupId", "TimeWindow"))
            {
                result.AddError(FlowValidationIssueCodes.CameraMatchModeInvalid, "MatchMode must be TriggerId, Any, ScanGroupId, or TimeWindow.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.MatchMode);
            }

            ValidateNonNegativeInt(node, FlowSettingNames.TimeoutMs, 1000, fieldPrefix, result);
            if (string.Equals(callbackMode, CameraCallbackModes.StreamFrames, StringComparison.OrdinalIgnoreCase))
            {
                var streamOutputMode = GetSettingString(node, FlowSettingNames.StreamOutputMode, CameraStreamOutputModes.Batch);
                if (!IsOneOf(streamOutputMode, CameraStreamOutputModes.Batch, CameraStreamOutputModes.PerFrame))
                {
                    result.AddError(FlowValidationIssueCodes.CameraStreamOutputModeInvalid, "StreamOutputMode must be Batch or PerFrame.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.StreamOutputMode);
                }

                if (string.Equals(streamOutputMode, CameraStreamOutputModes.PerFrame, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateNonNegativeInt(node, FlowSettingNames.ExpectedFrameCount, 1, fieldPrefix, result);
                    ValidateNonNegativeInt(node, FlowSettingNames.StartFrameIndex, 0, fieldPrefix, result);
                }
                else
                {
                    ValidatePositiveInt(node, FlowSettingNames.ExpectedFrameCount, 1, fieldPrefix, result);
                }

                ValidateNonNegativeInt(node, FlowSettingNames.FrameTimeoutMs, 1000, fieldPrefix, result);
            }
        }

        private static void ValidateQueueSettings(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            bool useQueue;
            var hasUseQueue = TryGetSettingBoolean(node, FlowSettingNames.UseQueue, out useQueue, result, fieldPrefix + FlowSettingNames.UseQueue);
            var validateQueue = hasUseQueue && useQueue;
            validateQueue = validateQueue || HasSetting(node, FlowSettingNames.QueueName) || HasSetting(node, FlowSettingNames.QueueCapacity) ||
                HasSetting(node, FlowSettingNames.QueueMaxDegreeOfParallelism) || HasSetting(node, FlowSettingNames.QueueFullMode) ||
                HasSetting(node, FlowSettingNames.WaitForCompletion);

            if (!validateQueue)
            {
                return;
            }

            if (useQueue && string.IsNullOrWhiteSpace(GetSettingString(node, FlowSettingNames.QueueName, FlowQueueNames.Default)))
            {
                result.AddError(FlowValidationIssueCodes.QueueNameMissing, "QueueName is required when UseQueue is true.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.QueueName);
            }

            ValidatePositiveInt(node, FlowSettingNames.QueueCapacity, 16, fieldPrefix, result);
            ValidatePositiveInt(node, FlowSettingNames.QueueMaxDegreeOfParallelism, 1, fieldPrefix, result);

            var fullMode = GetSettingString(node, FlowSettingNames.QueueFullMode, FlowQueueFullModeNames.Wait);
            if (!IsOneOf(fullMode, FlowQueueFullModeNames.Wait, FlowQueueFullModeNames.Reject, FlowQueueFullModeNames.Drop, FlowQueueFullModeNames.StopFlow, FlowQueueFullModeNames.NotifyOnly))
            {
                result.AddError(FlowValidationIssueCodes.QueueFullModeInvalid, "QueueFullMode must be Wait, Reject, Drop, StopFlow, or NotifyOnly.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.QueueFullMode);
            }

            bool waitForCompletion;
            if (HasSetting(node, FlowSettingNames.WaitForCompletion))
            {
                TryGetSettingBoolean(node, FlowSettingNames.WaitForCompletion, out waitForCompletion, result, fieldPrefix + FlowSettingNames.WaitForCompletion);
            }
        }

        private static void ValidateFrameGroupJoinNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            ValidatePositiveInt(node, FlowSettingNames.ExpectedShotCount, 2, fieldPrefix, result);
            ValidateNonNegativeInt(node, FlowSettingNames.TimeoutMs, 0, fieldPrefix, result);
            ValidateDuplicatePolicy(node, fieldPrefix, result);

            bool requireContinuous;
            if (TryGetSettingBoolean(node, FlowSettingNames.RequireContinuousShotIndex, out requireContinuous, result, fieldPrefix + FlowSettingNames.RequireContinuousShotIndex) &&
                requireContinuous)
            {
                ValidateNonNegativeInt(node, FlowSettingNames.FirstShotIndex, 0, fieldPrefix, result);
            }
        }

        private static void ValidateScanGroupJoinNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            ValidatePositiveInt(node, FlowSettingNames.ExpectedFrameCount, 2, fieldPrefix, result);
            ValidateNonNegativeInt(node, FlowSettingNames.TimeoutMs, 0, fieldPrefix, result);
            ValidateDuplicatePolicy(node, fieldPrefix, result);

            bool requireContinuous;
            if (TryGetSettingBoolean(node, FlowSettingNames.RequireContinuousFrameIndex, out requireContinuous, result, fieldPrefix + FlowSettingNames.RequireContinuousFrameIndex) &&
                requireContinuous)
            {
                ValidateNonNegativeInt(node, FlowSettingNames.FirstFrameIndex, 0, fieldPrefix, result);
            }
        }

        private static void ValidateDuplicatePolicy(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            var duplicatePolicy = GetSettingString(node, FlowSettingNames.DuplicatePolicy, FlowDuplicatePolicies.Error);
            if (!IsOneOf(duplicatePolicy, FlowDuplicatePolicies.Error, FlowDuplicatePolicies.Ignore, FlowDuplicatePolicies.Replace))
            {
                result.AddError(FlowValidationIssueCodes.DuplicatePolicyInvalid, "DuplicatePolicy must be Error, Ignore, or Replace.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.DuplicatePolicy);
            }
        }
    }
}
