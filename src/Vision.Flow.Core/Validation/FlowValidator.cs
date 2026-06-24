using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Vision.Flow.Core
{
    public sealed class FlowValidator
    {
        private readonly NodeRegistry _nodeRegistry;

        public FlowValidator(NodeRegistry nodeRegistry)
        {
            if (nodeRegistry == null)
            {
                throw new ArgumentNullException("nodeRegistry");
            }

            _nodeRegistry = nodeRegistry;
        }

        public FlowValidationResult Validate(FlowDesignDocument document)
        {
            var result = new FlowValidationResult();
            if (document == null)
            {
                result.AddError("FlowDesignMissing", "Flow design document is required.");
                return result;
            }

            if (document.Runtime == null)
            {
                result.AddError("RuntimeMissing", "Flow design document must contain a runtime definition.");
                return result;
            }

            return Validate(document.Runtime);
        }

        public FlowValidationResult Validate(RuntimeFlowDefinition definition)
        {
            var result = new FlowValidationResult();
            if (definition == null)
            {
                result.AddError("RuntimeMissing", "Runtime flow definition is required.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(definition.FlowId))
            {
                result.AddError("FlowIdMissing", "FlowId is required.", field: "FlowId");
            }

            var nodes = definition.Nodes ?? new List<NodeDefinition>();
            var nodeMap = BuildNodeMap(nodes, result);
            var descriptorsByNodeId = ValidateNodeFactories(nodes, result);

            if (nodes.Count == 0)
            {
                result.AddError("NodesMissing", "Runtime flow must contain at least one node.", field: "Nodes");
            }

            ValidateEdges(definition.Edges ?? new List<EdgeDefinition>(), nodeMap, descriptorsByNodeId, result);
            ValidateEntries(definition.Entries ?? new List<FlowEntryDefinition>(), nodeMap, result);
            ValidateRequiredSettings(nodes, descriptorsByNodeId, result);
            ValidateVariableBindings(nodes, nodeMap, descriptorsByNodeId, result);
            ValidateNoDesignerState(definition, result);

            return result;
        }

        private static Dictionary<string, NodeDefinition> BuildNodeMap(
            IList<NodeDefinition> nodes,
            FlowValidationResult result)
        {
            var nodeMap = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null)
                {
                    result.AddError("NodeMissing", "Node definition must not be null.", field: "Nodes[" + index + "]");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    result.AddError("NodeIdMissing", "NodeId is required.", field: "Nodes[" + index + "].Id");
                    continue;
                }

                if (nodeMap.ContainsKey(node.Id))
                {
                    result.AddError(
                        "NodeIdDuplicate",
                        "NodeId must be unique: " + node.Id,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Id");
                    continue;
                }

                nodeMap[node.Id] = node;
            }

            return nodeMap;
        }

        private Dictionary<string, NodeDescriptor> ValidateNodeFactories(
            IList<NodeDefinition> nodes,
            FlowValidationResult result)
        {
            var descriptorsByNodeId = new Dictionary<string, NodeDescriptor>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Type))
                {
                    result.AddError("NodeTypeMissing", "Node type is required.", nodeId: node.Id, field: "Nodes[" + index + "].Type");
                    continue;
                }

                INodeFactory factory;
                if (!_nodeRegistry.TryGetFactory(node.Type, out factory))
                {
                    result.AddError(
                        "NodeTypeNotRegistered",
                        "Node factory was not registered: " + node.Type,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Type");
                    continue;
                }

                var descriptor = factory.Descriptor;
                if (descriptor == null)
                {
                    result.AddError(
                        "NodeDescriptorMissing",
                        "Node factory must provide a descriptor: " + node.Type,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Type");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(descriptor.NodeType))
                {
                    result.AddError(
                        "NodeDescriptorTypeMissing",
                        "Node descriptor must provide NodeType: " + node.Type,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Type");
                }
                else if (!string.Equals(descriptor.NodeType, node.Type, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError(
                        "NodeDescriptorTypeMismatch",
                        "Node descriptor type does not match node type. Node=" + node.Type + ", Descriptor=" + descriptor.NodeType,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Type");
                }

                if (!descriptorsByNodeId.ContainsKey(node.Id))
                {
                    descriptorsByNodeId[node.Id] = descriptor;
                }
            }

            return descriptorsByNodeId;
        }

        private static void ValidateEdges(
            IList<EdgeDefinition> edges,
            IDictionary<string, NodeDefinition> nodeMap,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            for (var index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];
                if (edge == null)
                {
                    result.AddError("EdgeMissing", "Edge definition must not be null.", edgeIndex: index, field: "Edges[" + index + "]");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(edge.FromNodeId))
                {
                    result.AddError("EdgeSourceMissing", "Edge source node id is required.", edgeIndex: index, field: "Edges[" + index + "].FromNodeId");
                }

                if (string.IsNullOrWhiteSpace(edge.ToNodeId))
                {
                    result.AddError("EdgeTargetMissing", "Edge target node id is required.", edgeIndex: index, field: "Edges[" + index + "].ToNodeId");
                }

                if (string.IsNullOrWhiteSpace(edge.FromPort))
                {
                    result.AddError("EdgeFromPortMissing", "Edge source port is required.", edgeIndex: index, field: "Edges[" + index + "].FromPort");
                }

                if (string.IsNullOrWhiteSpace(edge.ToPort))
                {
                    result.AddError("EdgeToPortMissing", "Edge target port is required.", edgeIndex: index, field: "Edges[" + index + "].ToPort");
                }

                NodeDefinition fromNode;
                var hasFromNode = !string.IsNullOrWhiteSpace(edge.FromNodeId) && nodeMap.TryGetValue(edge.FromNodeId, out fromNode);
                if (!hasFromNode && !string.IsNullOrWhiteSpace(edge.FromNodeId))
                {
                    result.AddError(
                        "EdgeSourceMissing",
                        "Edge source node does not exist: " + edge.FromNodeId,
                        edgeIndex: index,
                        field: "Edges[" + index + "].FromNodeId");
                }

                NodeDefinition toNode;
                var hasToNode = !string.IsNullOrWhiteSpace(edge.ToNodeId) && nodeMap.TryGetValue(edge.ToNodeId, out toNode);
                if (!hasToNode && !string.IsNullOrWhiteSpace(edge.ToNodeId))
                {
                    result.AddError(
                        "EdgeTargetMissing",
                        "Edge target node does not exist: " + edge.ToNodeId,
                        edgeIndex: index,
                        field: "Edges[" + index + "].ToNodeId");
                }

                if (hasFromNode && !string.IsNullOrWhiteSpace(edge.FromPort))
                {
                    NodeDescriptor descriptor;
                    if (descriptorsByNodeId.TryGetValue(edge.FromNodeId, out descriptor) &&
                        !ContainsPort(descriptor.OutputPorts, edge.FromPort))
                    {
                        result.AddError(
                            "EdgeFromPortUnknown",
                            "Edge source port does not exist on node descriptor. Node=" + edge.FromNodeId + ", Port=" + edge.FromPort,
                            nodeId: edge.FromNodeId,
                            edgeIndex: index,
                            field: "Edges[" + index + "].FromPort");
                    }
                }

                if (hasToNode && !string.IsNullOrWhiteSpace(edge.ToPort))
                {
                    NodeDescriptor descriptor;
                    if (descriptorsByNodeId.TryGetValue(edge.ToNodeId, out descriptor) &&
                        !ContainsPort(descriptor.InputPorts, edge.ToPort))
                    {
                        result.AddError(
                            "EdgeToPortUnknown",
                            "Edge target port does not exist on node descriptor. Node=" + edge.ToNodeId + ", Port=" + edge.ToPort,
                            nodeId: edge.ToNodeId,
                            edgeIndex: index,
                            field: "Edges[" + index + "].ToPort");
                    }
                }
            }
        }

        private static void ValidateEntries(
            IList<FlowEntryDefinition> entries,
            IDictionary<string, NodeDefinition> nodeMap,
            FlowValidationResult result)
        {
            if (entries.Count == 0)
            {
                result.AddError("EntriesMissing", "Runtime flow must contain at least one entry.", field: "Entries");
                return;
            }

            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    result.AddError("EntryMissing", "Entry definition must not be null.", field: "Entries[" + index + "]");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.EntryName))
                {
                    result.AddError("EntryNameMissing", "Entry name is required.", field: "Entries[" + index + "].EntryName");
                }
                else if (!entryNames.Add(entry.EntryName))
                {
                    result.AddError(
                        "EntryNameDuplicate",
                        "Entry name must be unique: " + entry.EntryName,
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].EntryName");
                }

                if (string.IsNullOrWhiteSpace(entry.TargetNodeId))
                {
                    result.AddError(
                        "EntryTargetMissing",
                        "Entry target node id is required.",
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].TargetNodeId");
                }
                else if (!nodeMap.ContainsKey(entry.TargetNodeId))
                {
                    result.AddError(
                        "EntryTargetMissing",
                        "Entry target node does not exist: " + entry.TargetNodeId,
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].TargetNodeId");
                }
            }
        }

        private static void ValidateRequiredSettings(
            IList<NodeDefinition> nodes,
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

                NodeDescriptor descriptor;
                if (!descriptorsByNodeId.TryGetValue(node.Id, out descriptor) || descriptor.Settings == null)
                {
                    continue;
                }

                for (var settingIndex = 0; settingIndex < descriptor.Settings.Count; settingIndex++)
                {
                    var setting = descriptor.Settings[settingIndex];
                    if (setting == null || !setting.IsRequired || string.IsNullOrWhiteSpace(setting.Name))
                    {
                        continue;
                    }

                    if (!HasConfiguredValue(node, setting.Name))
                    {
                        result.AddError(
                            "RequiredSettingMissing",
                            "Required setting is missing or empty. Node=" + node.Id + ", Setting=" + setting.Name,
                            nodeId: node.Id,
                            field: "Nodes[" + nodeIndex + "].Settings." + setting.Name);
                    }
                }
            }
        }

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
                result.AddError("BindingInvalid", "Variable binding must not be null.", nodeId: nodeId, field: field);
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
                        "BindingInvalid",
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
            string sourceNodeId;
            string sourceOutputName;
            if (!VariableBinding.TryParseVariablePath(expression, out sourceNodeId, out sourceOutputName))
            {
                result.AddError(
                    "BindingInvalid",
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
                result.AddError("BindingInvalid", "Variable binding source node and output are required.", nodeId: nodeId, field: field);
                return;
            }

            NodeDefinition sourceNode;
            if (!nodeMap.TryGetValue(sourceNodeId, out sourceNode))
            {
                result.AddError(
                    "BindingSourceNodeMissing",
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
                    "BindingOutputMissing",
                    "Variable binding source output does not exist. Source=" + sourceNodeId + "." + sourceOutputName,
                    nodeId: nodeId,
                    field: field);
            }
        }

        private static void ValidateNoDesignerState(RuntimeFlowDefinition definition, FlowValidationResult result)
        {
            CheckNoDesignerStateValue("Settings", definition.Settings, result, 0);

            var nodes = definition.Nodes ?? new List<NodeDefinition>();
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null)
                {
                    continue;
                }

                CheckNoDesignerStateValue("Nodes[" + index + "].Settings", node.Settings, result, 0);
                if (node.InputBindings == null)
                {
                    continue;
                }

                foreach (var binding in node.InputBindings)
                {
                    if (binding.Value != null && binding.Value.IsConstant)
                    {
                        CheckNoDesignerStateValue("Nodes[" + index + "].InputBindings." + binding.Key + ".ConstantValue", binding.Value.ConstantValue, result, 0);
                    }
                }
            }
        }

        private static void CheckNoDesignerStateValue(
            string field,
            object value,
            FlowValidationResult result,
            int depth)
        {
            if (value == null || depth > 8 || IsSimpleValue(value))
            {
                return;
            }

            if (value is FlowDesignDocument || value is FlowViewState || value is NodeViewState)
            {
                result.AddError(
                    "RuntimeContainsViewState",
                    "Runtime flow must not contain designer view state.",
                    field: field);
                return;
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry item in dictionary)
                {
                    var key = Convert.ToString(item.Key, CultureInfo.InvariantCulture);
                    CheckNoDesignerStateValue(field + "." + key, item.Value, result, depth + 1);
                }

                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    CheckNoDesignerStateValue(field + "[" + index + "]", item, result, depth + 1);
                    index++;
                }
            }
        }

        private static bool HasConfiguredValue(NodeDefinition node, string settingName)
        {
            object value;
            if (TryGetIgnoreCase(node.Settings, settingName, out value) && !IsValueMissing(value))
            {
                return true;
            }

            VariableBinding binding;
            return TryGetIgnoreCase(node.InputBindings, settingName, out binding) && !IsBindingMissing(binding);
        }

        private static bool IsValueMissing(object value)
        {
            if (value == null)
            {
                return true;
            }

            var text = value as string;
            if (text != null)
            {
                return string.IsNullOrWhiteSpace(text);
            }

            var binding = value as VariableBinding;
            if (binding != null)
            {
                return IsBindingMissing(binding);
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                return dictionary.Count == 0;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (var item in enumerable)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static bool IsBindingMissing(VariableBinding binding)
        {
            if (binding == null)
            {
                return true;
            }

            if (binding.IsConstant)
            {
                return IsValueMissing(binding.ConstantValue);
            }

            if (!string.IsNullOrWhiteSpace(binding.SourceNodeId) && !string.IsNullOrWhiteSpace(binding.SourceOutputName))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(binding.Expression);
        }

        private static bool ContainsPort(IList<NodePortDescriptor> ports, string portName)
        {
            return ports != null && ports.Any(x => x != null && string.Equals(x.Name, portName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsOutput(IList<NodeOutputDescriptor> outputs, string outputName)
        {
            return outputs != null && outputs.Any(x => x != null && string.Equals(x.Name, outputName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetIgnoreCase<TValue>(
            IDictionary<string, TValue> dictionary,
            string key,
            out TValue value)
        {
            value = default(TValue);
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (var item in dictionary)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeTemplateBinding(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            return trimmed.StartsWith("{{", StringComparison.Ordinal) && trimmed.EndsWith("}}", StringComparison.Ordinal);
        }

        private static bool LooksLikeBindingField(string field)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return false;
            }

            return field.EndsWith(".ValueBinding", StringComparison.OrdinalIgnoreCase) ||
                field.EndsWith(".Binding", StringComparison.OrdinalIgnoreCase) ||
                field.EndsWith(".Expression", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSimpleValue(object value)
        {
            if (value == null)
            {
                return true;
            }

            var type = value.GetType();
            return type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) ||
                type == typeof(Guid);
        }
    }
}
