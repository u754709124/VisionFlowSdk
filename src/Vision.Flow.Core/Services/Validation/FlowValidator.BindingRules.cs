using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
        private static void ValidateVariableBindings(
            IList<NodeDefinition> nodes,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                if (node.InputBindings != null)
                {
                    foreach (var binding in node.InputBindings)
                    {
                        ValidateBinding(
                            node.Id,
                            "Nodes[" + nodeIndex + "].InputBindings." + binding.Key,
                            binding.Value,
                            nodeMap,
                            descriptorsByNodeId,
                            result);
                    }
                }

                if (node.Settings != null)
                {
                    foreach (var setting in node.Settings)
                    {
                        CollectAndValidateBindingValues(
                            node.Id,
                            "Nodes[" + nodeIndex + "].Settings." + setting.Key,
                            setting.Value,
                            nodeMap,
                            descriptorsByNodeId,
                            result,
                            0);
                    }
                }
            }
        }

        private static void CollectAndValidateBindingValues(
            string nodeId,
            string field,
            object value,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result,
            int depth)
        {
            if (value == null || depth > 8)
            {
                return;
            }

            var binding = value as VariableBinding;
            if (binding != null)
            {
                ValidateBinding(nodeId, field, binding, nodeMap, descriptorsByNodeId, result);
                return;
            }

            var text = value as string;
            if (text != null)
            {
                if (LooksLikeTemplateBinding(text) || LooksLikeBindingField(field))
                {
                    ValidateBindingExpression(nodeId, field, text, nodeMap, descriptorsByNodeId, result);
                }

                return;
            }

            if (IsSimpleValue(value))
            {
                return;
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry item in dictionary)
                {
                    var key = Convert.ToString(item.Key, CultureInfo.InvariantCulture);
                    CollectAndValidateBindingValues(
                        nodeId,
                        field + "." + key,
                        item.Value,
                        nodeMap,
                        descriptorsByNodeId,
                        result,
                        depth + 1);
                }

                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    CollectAndValidateBindingValues(
                        nodeId,
                        field + "[" + index + "]",
                        item,
                        nodeMap,
                        descriptorsByNodeId,
                        result,
                        depth + 1);
                    index++;
                }

                return;
            }

            var properties = value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (var index = 0; index < properties.Length; index++)
            {
                var property = properties[index];
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object propertyValue;
                try
                {
                    propertyValue = property.GetValue(value, null);
                }
                catch (TargetInvocationException)
                {
                    continue;
                }

                CollectAndValidateBindingValues(
                    nodeId,
                    field + "." + property.Name,
                    propertyValue,
                    nodeMap,
                    descriptorsByNodeId,
                    result,
                    depth + 1);
            }
        }

        private static void ValidateBinding(
            string nodeId,
            string field,
            VariableBinding binding,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            if (binding == null)
            {
                result.AddError(FlowValidationIssueCodes.BindingInvalid, "Variable binding must not be null.", nodeId: nodeId, field: field);
                return;
            }

            if (binding.IsConstant)
            {
                return;
            }

            var sourceNodeId = binding.SourceNodeId;
            var sourceOutputName = binding.SourceOutputName;
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(sourceOutputName))
            {
                if (!VariableBinding.TryParseVariablePath(binding.Expression, out sourceNodeId, out sourceOutputName))
                {
                    result.AddError(
                        FlowValidationIssueCodes.BindingInvalid,
                        "Variable binding must reference a source node output: " + binding.Expression,
                        nodeId: nodeId,
                        field: field);
                    return;
                }
            }

            ValidateBindingReference(nodeId, field, sourceNodeId, sourceOutputName, nodeMap, descriptorsByNodeId, result);
        }

        private static void ValidateBindingExpression(
            string nodeId,
            string field,
            string expression,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            if (IsTokenBindingExpression(expression))
            {
                return;
            }

            string sourceNodeId;
            string sourceOutputName;
            if (!VariableBinding.TryParseVariablePath(expression, out sourceNodeId, out sourceOutputName))
            {
                result.AddError(
                    FlowValidationIssueCodes.BindingInvalid,
                    "Variable binding expression is invalid: " + expression,
                    nodeId: nodeId,
                    field: field);
                return;
            }

            ValidateBindingReference(nodeId, field, sourceNodeId, sourceOutputName, nodeMap, descriptorsByNodeId, result);
        }

        private static void ValidateBindingReference(
            string nodeId,
            string field,
            string sourceNodeId,
            string sourceOutputName,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId) || string.IsNullOrWhiteSpace(sourceOutputName))
            {
                result.AddError(FlowValidationIssueCodes.BindingInvalid, "Variable binding source node and output are required.", nodeId: nodeId, field: field);
                return;
            }

            NodeDefinition sourceNode;
            if (!nodeMap.TryGetValue(sourceNodeId, out sourceNode))
            {
                result.AddError(
                    FlowValidationIssueCodes.BindingSourceNodeMissing,
                    "Variable binding source node does not exist: " + sourceNodeId,
                    nodeId: nodeId,
                    field: field);
                return;
            }

            NodeDescriptor descriptor;
            if (descriptorsByNodeId.TryGetValue(sourceNode.Id, out descriptor) &&
                !ContainsOutput(descriptor.Outputs, sourceOutputName))
            {
                result.AddError(
                    FlowValidationIssueCodes.BindingOutputMissing,
                    "Variable binding source output does not exist. Source=" + sourceNodeId + "." + sourceOutputName,
                    nodeId: nodeId,
                    field: field);
            }
        }
    }
}
