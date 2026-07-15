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
                    ValidateSettingValue(node, field, item.Value, settingDescriptor, edges, nodeMap, descriptorsByNodeId, result);
                }
            }
        }

        private static void ValidateSettingValue(
            NodeDefinition node,
            string field,
            NodeSettingValue setting,
            NodeSettingDescriptor descriptor,
            IList<EdgeDefinition> edges,
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
                result.AddError(FlowValidationIssueCodes.VariableSelectorInvalid, "TriggerInput selectors are reserved and cannot be published yet.", nodeId: node.Id, field: field + ".Selector.Scope");
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
    }
}
