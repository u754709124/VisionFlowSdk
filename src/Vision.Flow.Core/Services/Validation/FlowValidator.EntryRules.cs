using System;
using System.Collections.Generic;
using System.Globalization;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
        private void ValidateEntries(
            IList<FlowEntryDefinition> entries,
            IDictionary<string, NodeDefinition> nodeMap,
            FlowValidationResult result)
        {
            if (entries.Count == 0)
            {
                result.AddError(FlowValidationIssueCodes.EntriesMissing, "Runtime flow must contain at least one entry.", field: "Entries");
                return;
            }

            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var listenerSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var field = "Entries[" + index + "]";
                if (entry == null)
                {
                    result.AddError(FlowValidationIssueCodes.EntryMissing, "Entry definition must not be null.", field: field);
                    continue;
                }

                ValidateEntryName(entry, field, entryNames, result);
                if (!Enum.IsDefined(typeof(FlowTriggerKind), entry.TriggerKind))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryTriggerKindInvalid,
                        "Entry TriggerKind is invalid.",
                        entryName: entry.EntryName,
                        field: field + ".TriggerKind");
                }
                else if (entry.TriggerKind == FlowTriggerKind.NodeEvent)
                {
                    ValidateNodeEventEntry(entry, field, nodeMap, listenerSourceIds, result);
                }
                else
                {
                    ValidateCallableEntry(entry, field, nodeMap, result);
                }

                ValidateTriggerInputs(entry, field, result);
                ValidateTriggerExecutionPolicy(entry, field, result);
            }
        }

        private static void ValidateEntryName(
            FlowEntryDefinition entry,
            string field,
            ISet<string> entryNames,
            FlowValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(entry.EntryName))
            {
                result.AddError(FlowValidationIssueCodes.EntryNameMissing, "Entry name is required.", field: field + ".EntryName");
            }
            else if (!entryNames.Add(entry.EntryName))
            {
                result.AddError(
                    FlowValidationIssueCodes.EntryNameDuplicate,
                    "Entry name must be unique: " + entry.EntryName,
                    entryName: entry.EntryName,
                    field: field + ".EntryName");
            }
        }

        private static void ValidateCallableEntry(
            FlowEntryDefinition entry,
            string field,
            IDictionary<string, NodeDefinition> nodeMap,
            FlowValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(entry.TargetNodeId))
            {
                result.AddError(
                    FlowValidationIssueCodes.EntryTargetMissing,
                    "Manual and External entries require TargetNodeId.",
                    entryName: entry.EntryName,
                    field: field + ".TargetNodeId");
            }
            else if (!nodeMap.ContainsKey(entry.TargetNodeId))
            {
                result.AddError(
                    FlowValidationIssueCodes.EntryTargetNotFound,
                    "Entry target node does not exist: " + entry.TargetNodeId,
                    entryName: entry.EntryName,
                    field: field + ".TargetNodeId");
            }
        }

        private void ValidateNodeEventEntry(
            FlowEntryDefinition entry,
            string field,
            IDictionary<string, NodeDefinition> nodeMap,
            ISet<string> listenerSourceIds,
            FlowValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceNodeId))
            {
                result.AddError(
                    FlowValidationIssueCodes.EntrySourceMissing,
                    "NodeEvent entry SourceNodeId is required.",
                    entryName: entry.EntryName,
                    field: field + ".SourceNodeId");
                return;
            }

            if (!listenerSourceIds.Add(entry.SourceNodeId))
            {
                result.AddError(
                    FlowValidationIssueCodes.EntrySourceDuplicate,
                    "A listener source node can only be assigned to one NodeEvent entry: " + entry.SourceNodeId,
                    entryName: entry.EntryName,
                    field: field + ".SourceNodeId");
            }

            NodeDefinition sourceNode;
            if (!nodeMap.TryGetValue(entry.SourceNodeId, out sourceNode))
            {
                result.AddError(
                    FlowValidationIssueCodes.EntrySourceNotFound,
                    "NodeEvent source node does not exist: " + entry.SourceNodeId,
                    entryName: entry.EntryName,
                    field: field + ".SourceNodeId");
                return;
            }

            try
            {
                if (!(_nodeRegistry.CreateNode(sourceNode) is IFlowListenerNode))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntrySourceNotListener,
                        "NodeEvent source node must implement IFlowListenerNode: " + entry.SourceNodeId,
                        entryName: entry.EntryName,
                        field: field + ".SourceNodeId");
                }
            }
            catch (Exception)
            {
                // 节点工厂相关错误由节点契约校验报告，避免在入口规则重复暴露实现异常。
            }
        }

        private static void ValidateTriggerInputs(
            FlowEntryDefinition entry,
            string field,
            FlowValidationResult result)
        {
            if (entry.Inputs == null)
            {
                result.AddError(
                    FlowValidationIssueCodes.EntryInputInvalid,
                    "Entry Inputs collection must not be null.",
                    entryName: entry.EntryName,
                    field: field + ".Inputs");
                return;
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var inputIndex = 0; inputIndex < entry.Inputs.Count; inputIndex++)
            {
                var input = entry.Inputs[inputIndex];
                var inputField = field + ".Inputs[" + inputIndex + "]";
                if (input == null)
                {
                    result.AddError(FlowValidationIssueCodes.EntryInputInvalid, "Trigger input must not be null.", entryName: entry.EntryName, field: inputField);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(input.Name))
                {
                    result.AddError(FlowValidationIssueCodes.EntryInputInvalid, "Trigger input Name is required.", entryName: entry.EntryName, field: inputField + ".Name");
                }
                else if (!names.Add(input.Name))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryInputNameDuplicate,
                        "Trigger input Name must be unique within an entry: " + input.Name,
                        entryName: entry.EntryName,
                        field: inputField + ".Name");
                }

                if (!Enum.IsDefined(typeof(FlowDataType), input.DataType) || input.DataType == FlowDataType.Control)
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryInputInvalid,
                        "Trigger input DataType must be a non-control data type.",
                        entryName: entry.EntryName,
                        field: inputField + ".DataType");
                }
                else if (input.DefaultValue != null && !CanConvertTriggerDefault(input.DefaultValue, input.DataType))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryInputDefaultInvalid,
                        "Trigger input default value cannot be converted to " + input.DataType + ".",
                        entryName: entry.EntryName,
                        field: inputField + ".DefaultValue");
                }
            }
        }

        private static void ValidateTriggerExecutionPolicy(
            FlowEntryDefinition entry,
            string field,
            FlowValidationResult result)
        {
            var policy = entry.ExecutionPolicy;
            if (policy == null)
            {
                result.AddError(
                    FlowValidationIssueCodes.EntryExecutionPolicyInvalid,
                    "Entry ExecutionPolicy must not be null.",
                    entryName: entry.EntryName,
                    field: field + ".ExecutionPolicy");
                return;
            }

            if (policy.MaxConcurrentRuns <= 0)
            {
                result.AddError(FlowValidationIssueCodes.EntryExecutionPolicyInvalid, "MaxConcurrentRuns must be greater than zero.", entryName: entry.EntryName, field: field + ".ExecutionPolicy.MaxConcurrentRuns");
            }

            if (policy.QueueCapacity < 0)
            {
                result.AddError(FlowValidationIssueCodes.EntryExecutionPolicyInvalid, "QueueCapacity must not be negative.", entryName: entry.EntryName, field: field + ".ExecutionPolicy.QueueCapacity");
            }

            if (!Enum.IsDefined(typeof(TriggerQueueFullBehavior), policy.QueueFullBehavior))
            {
                result.AddError(FlowValidationIssueCodes.EntryExecutionPolicyInvalid, "QueueFullBehavior is invalid.", entryName: entry.EntryName, field: field + ".ExecutionPolicy.QueueFullBehavior");
            }
        }

        private static bool CanConvertTriggerDefault(object value, FlowDataType dataType)
        {
            try
            {
                switch (dataType)
                {
                    case FlowDataType.String:
                        Convert.ToString(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Int32:
                        Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Int64:
                        Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Boolean:
                        Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Double:
                        Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.DateTime:
                        Convert.ToDateTime(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.IVisionImage:
                        return value is IVisionImage;
                    case FlowDataType.CameraFrameData:
                        return value is CameraFrameData;
                    case FlowDataType.Object:
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
