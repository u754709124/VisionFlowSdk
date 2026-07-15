using System;
using System.Collections.Generic;
using System.Linq;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
        private static void ValidateSettingValues(
            IList<NodeDefinition> nodes,
            IList<EdgeDefinition> edges,
            IList<FlowEntryDefinition> entries,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || node.Settings == null)
                {
                    continue;
                }

                NodeDescriptor targetDescriptor;
                descriptorsByNodeId.TryGetValue(node.Id, out targetDescriptor);
                foreach (var item in node.Settings)
                {
                    var field = "Nodes[" + nodeIndex + "].Settings." + item.Key;
                    var settingDescriptor = FindSetting(targetDescriptor, item.Key);
                    ValidateSettingValue(node, field, item.Value, settingDescriptor, edges, entries, nodeMap, descriptorsByNodeId, result);
                }
            }
        }

        private static void ValidateSettingValue(
            NodeDefinition node,
            string field,
            NodeSettingValue setting,
            NodeSettingDescriptor descriptor,
            IList<EdgeDefinition> edges,
            IList<FlowEntryDefinition> entries,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            if (setting == null)
            {
                result.AddError(FlowValidationIssueCodes.SettingValueInvalid, "Node setting value must not be null.", nodeId: node.Id, field: field);
                return;
            }

            if (setting.Mode == NodeSettingValueMode.Constant)
            {
                return;
            }

            if (setting.Mode != NodeSettingValueMode.Variable)
            {
                result.AddError(FlowValidationIssueCodes.SettingValueInvalid, "Node setting Mode must be Constant or Variable.", nodeId: node.Id, field: field + ".Mode");
                return;
            }

            if (descriptor == null)
            {
                result.AddError(FlowValidationIssueCodes.VariableSelectorInvalid, "Variable setting does not have a matching setting descriptor.", nodeId: node.Id, field: field);
                return;
            }

            if (descriptor.BindingMode != NodeSettingBindingMode.ConstantOrVariable)
            {
                result.AddError(FlowValidationIssueCodes.VariableSelectorNotAllowed, "This setting only accepts a constant value.", nodeId: node.Id, field: field);
                return;
            }

            var selector = setting.Selector;
            if (selector == null || selector.Path == null || selector.Path.Count == 0 || selector.Path.Any(string.IsNullOrWhiteSpace))
            {
                result.AddError(FlowValidationIssueCodes.VariableSelectorInvalid, "Variable selector Path is required.", nodeId: node.Id, field: field + ".Selector");
                return;
            }

            if (!IsScopeAllowed(descriptor.AllowedVariableSources, selector.Scope))
            {
                result.AddError(FlowValidationIssueCodes.VariableSelectorNotAllowed, "Variable selector scope is not allowed for this setting: " + selector.Scope, nodeId: node.Id, field: field + ".Selector.Scope");
                return;
            }

            if (selector.Scope == VariableSelectorScope.NodeOutput)
            {
                ValidateNodeOutputSelector(node, field, selector, descriptor, edges, nodeMap, descriptorsByNodeId, result);
                return;
            }

            if (selector.Scope == VariableSelectorScope.TriggerInput)
            {
                ValidateTriggerInputSelector(node, field, selector, descriptor, edges, entries, result);
            }
        }

        private static void ValidateTriggerInputSelector(
            NodeDefinition node,
            string field,
            VariableSelector selector,
            NodeSettingDescriptor targetSetting,
            IList<EdgeDefinition> edges,
            IList<FlowEntryDefinition> entries,
            FlowValidationResult result)
        {
            var inputName = selector.Path[0];
            var reachableInputs = new List<TriggerInputDescriptor>();
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                var entry = entries[entryIndex];
                if (entry == null || entry.Inputs == null || !CanEntryReachNode(entry, node.Id, edges))
                {
                    continue;
                }

                var input = entry.Inputs.FirstOrDefault(
                    x => x != null && string.Equals(x.Name, inputName, StringComparison.OrdinalIgnoreCase));
                if (input != null)
                {
                    reachableInputs.Add(input);
                }
            }

            if (reachableInputs.Count == 0)
            {
                result.AddError(
                    FlowValidationIssueCodes.TriggerInputUnavailable,
                    "No entry that declares trigger input '" + inputName + "' can reach this node.",
                    nodeId: node.Id,
                    field: field + ".Selector.Path");
                return;
            }

            var sourceTypes = new HashSet<FlowDataType>();
            for (var index = 0; index < reachableInputs.Count; index++)
            {
                sourceTypes.Add(reachableInputs[index].DataType);
            }

            if (sourceTypes.Count > 1)
            {
                result.AddError(
                    FlowValidationIssueCodes.TriggerInputTypeConflict,
                    "Reachable entries declare trigger input '" + inputName + "' with conflicting data types.",
                    nodeId: node.Id,
                    field: field + ".Selector.Path");
                return;
            }

            var sourceType = sourceTypes.First();
            if (selector.Path.Count > 1 && sourceType != FlowDataType.Object)
            {
                result.AddError(
                    FlowValidationIssueCodes.VariableTypeIncompatible,
                    "Nested TriggerInput paths require the declared input type Object.",
                    nodeId: node.Id,
                    field: field + ".Selector.Path");
                return;
            }

            var compatibility = FlowDataTypeCompatibility.GetCompatibility(sourceType, targetSetting.DataType);
            if (compatibility == FlowDataTypeCompatibilityResult.Incompatible)
            {
                result.AddError(
                    FlowValidationIssueCodes.VariableTypeIncompatible,
                    "Trigger input type " + sourceType + " cannot be assigned to setting type " + targetSetting.DataType + ".",
                    nodeId: node.Id,
                    field: field);
            }
            else if (compatibility == FlowDataTypeCompatibilityResult.Warning)
            {
                result.AddWarning(
                    FlowValidationIssueCodes.VariableTypeWarning,
                    "Trigger input type " + sourceType + " will be checked against " + targetSetting.DataType + " at runtime.",
                    nodeId: node.Id,
                    field: field);
            }
        }

        private static void ValidateNodeOutputSelector(
            NodeDefinition node,
            string field,
            VariableSelector selector,
            NodeSettingDescriptor targetSetting,
            IList<EdgeDefinition> edges,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            if (selector.Path.Count < 2)
            {
                result.AddError(FlowValidationIssueCodes.VariableSelectorInvalid, "NodeOutput selector Path must contain node id and output name.", nodeId: node.Id, field: field + ".Selector.Path");
                return;
            }

            var sourceNodeId = selector.Path[0];
            var sourceOutputName = selector.Path[1];
            NodeDefinition sourceNode;
            if (!nodeMap.TryGetValue(sourceNodeId, out sourceNode))
            {
                result.AddError(FlowValidationIssueCodes.VariableSourceNodeMissing, "Variable source node does not exist: " + sourceNodeId, nodeId: node.Id, field: field);
                return;
            }

            if (!IsUpstream(sourceNodeId, node.Id, edges))
            {
                result.AddError(FlowValidationIssueCodes.VariableSourceNotUpstream, "Variable source must be an upstream node connected by control-flow edges: " + sourceNodeId, nodeId: node.Id, field: field);
                return;
            }

            NodeDescriptor sourceDescriptor;
            if (!descriptorsByNodeId.TryGetValue(sourceNodeId, out sourceDescriptor))
            {
                return;
            }

            var output = sourceDescriptor.Outputs == null
                ? null
                : sourceDescriptor.Outputs.FirstOrDefault(x => x != null && string.Equals(x.Name, sourceOutputName, StringComparison.OrdinalIgnoreCase));
            if (output == null)
            {
                result.AddError(FlowValidationIssueCodes.VariableOutputMissing, "Variable source output does not exist: " + sourceNodeId + "." + sourceOutputName, nodeId: node.Id, field: field);
                return;
            }

            var compatibility = FlowDataTypeCompatibility.GetCompatibility(output.DataType, targetSetting.DataType);
            if (compatibility == FlowDataTypeCompatibilityResult.Incompatible)
            {
                result.AddError(FlowValidationIssueCodes.VariableTypeIncompatible, "Variable output type " + output.DataType + " cannot be assigned to setting type " + targetSetting.DataType + ".", nodeId: node.Id, field: field);
            }
            else if (compatibility == FlowDataTypeCompatibilityResult.Warning)
            {
                result.AddWarning(FlowValidationIssueCodes.VariableTypeWarning, "Variable output type Object will be checked against " + targetSetting.DataType + " at runtime.", nodeId: node.Id, field: field);
            }
        }

        private static NodeSettingDescriptor FindSetting(NodeDescriptor descriptor, string name)
        {
            return descriptor == null || descriptor.Settings == null
                ? null
                : descriptor.Settings.FirstOrDefault(x => x != null && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsScopeAllowed(VariableSelectorScopeFlags flags, VariableSelectorScope scope)
        {
            var value = scope == VariableSelectorScope.NodeOutput
                ? VariableSelectorScopeFlags.NodeOutput
                : scope == VariableSelectorScope.TriggerInput
                    ? VariableSelectorScopeFlags.TriggerInput
                    : VariableSelectorScopeFlags.Token;
            return (flags & value) == value;
        }

        private static bool IsUpstream(string sourceNodeId, string targetNodeId, IList<EdgeDefinition> edges)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Queue<string>();
            pending.Enqueue(targetNodeId);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                for (var index = 0; index < edges.Count; index++)
                {
                    var edge = edges[index];
                    if (edge == null || !string.Equals(edge.ToNodeId, current, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.Equals(edge.FromNodeId, sourceNodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    pending.Enqueue(edge.FromNodeId);
                }
            }

            return false;
        }

        private static bool CanEntryReachNode(
            FlowEntryDefinition entry,
            string targetNodeId,
            IList<EdgeDefinition> edges)
        {
            var pending = new Queue<string>();
            if (entry.TriggerKind == FlowTriggerKind.NodeEvent)
            {
                for (var index = 0; index < edges.Count; index++)
                {
                    var edge = edges[index];
                    if (edge != null && string.Equals(edge.FromNodeId, entry.SourceNodeId, StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Enqueue(edge.ToNodeId);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(entry.TargetNodeId))
            {
                pending.Enqueue(entry.TargetNodeId);
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (string.Equals(current, targetNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                for (var index = 0; index < edges.Count; index++)
                {
                    var edge = edges[index];
                    if (edge != null && string.Equals(edge.FromNodeId, current, StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Enqueue(edge.ToNodeId);
                    }
                }
            }

            return false;
        }
    }
}
