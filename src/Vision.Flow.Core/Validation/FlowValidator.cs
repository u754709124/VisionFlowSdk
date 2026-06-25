using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Vision.Flow.Core
{
    // 入口校验按运行时执行顺序组织高层检查。
    public sealed partial class FlowValidator
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
                result.AddError(FlowValidationIssueCodes.FlowDesignMissing, "Flow design document is required.");
                return result;
            }

            if (document.Runtime == null)
            {
                result.AddError(FlowValidationIssueCodes.RuntimeMissing, "Flow design document must contain a runtime definition.");
                return result;
            }

            return Validate(document.Runtime);
        }

        public FlowValidationResult Validate(RuntimeFlowDefinition definition)
        {
            var result = new FlowValidationResult();
            if (definition == null)
            {
                result.AddError(FlowValidationIssueCodes.RuntimeMissing, "Runtime flow definition is required.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(definition.FlowId))
            {
                result.AddError(FlowValidationIssueCodes.FlowIdMissing, "FlowId is required.", field: "FlowId");
            }

            var nodes = definition.Nodes ?? new List<NodeDefinition>();
            var nodeMap = BuildNodeMap(nodes, result);
            var descriptorsByNodeId = ValidateNodeFactories(nodes, result);

            if (nodes.Count == 0)
            {
                result.AddError(FlowValidationIssueCodes.NodesMissing, "Runtime flow must contain at least one node.", field: "Nodes");
            }

            ValidateEdges(definition.Edges ?? new List<EdgeDefinition>(), nodeMap, descriptorsByNodeId, result);
            ValidateEntries(definition.Entries ?? new List<FlowEntryDefinition>(), nodeMap, result);
            ValidateRequiredSettings(nodes, descriptorsByNodeId, result);
            ValidateVariableBindings(nodes, nodeMap, descriptorsByNodeId, result);
            ValidateNoDesignerState(definition, result);
            ValidateNodeSpecificRules(nodes, result);

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
                    result.AddError(FlowValidationIssueCodes.NodeMissing, "Node definition must not be null.", field: "Nodes[" + index + "]");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    result.AddError(FlowValidationIssueCodes.NodeIdMissing, "NodeId is required.", field: "Nodes[" + index + "].Id");
                    continue;
                }

                if (nodeMap.ContainsKey(node.Id))
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeIdDuplicate,
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
                    result.AddError(FlowValidationIssueCodes.NodeTypeMissing, "Node type is required.", nodeId: node.Id, field: "Nodes[" + index + "].Type");
                    continue;
                }

                INodeFactory factory;
                if (!_nodeRegistry.TryGetFactory(node.Type, out factory))
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeTypeNotRegistered,
                        "Node factory was not registered: " + node.Type,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Type");
                    continue;
                }

                var descriptor = factory.Descriptor;
                if (descriptor == null)
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeDescriptorMissing,
                        "Node factory must provide a descriptor: " + node.Type,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Type");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(descriptor.NodeType))
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeDescriptorTypeMissing,
                        "Node descriptor must provide NodeType: " + node.Type,
                        nodeId: node.Id,
                        field: "Nodes[" + index + "].Type");
                }
                else if (!string.Equals(descriptor.NodeType, node.Type, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeDescriptorTypeMismatch,
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
                    result.AddError(FlowValidationIssueCodes.EdgeMissing, "Edge definition must not be null.", edgeIndex: index, field: "Edges[" + index + "]");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(edge.FromNodeId))
                {
                    result.AddError(FlowValidationIssueCodes.EdgeSourceMissing, "Edge source node id is required.", edgeIndex: index, field: "Edges[" + index + "].FromNodeId");
                }

                if (string.IsNullOrWhiteSpace(edge.ToNodeId))
                {
                    result.AddError(FlowValidationIssueCodes.EdgeTargetMissing, "Edge target node id is required.", edgeIndex: index, field: "Edges[" + index + "].ToNodeId");
                }

                if (string.IsNullOrWhiteSpace(edge.FromPort))
                {
                    result.AddError(FlowValidationIssueCodes.EdgeFromPortMissing, "Edge source port is required.", edgeIndex: index, field: "Edges[" + index + "].FromPort");
                }

                if (string.IsNullOrWhiteSpace(edge.ToPort))
                {
                    result.AddError(FlowValidationIssueCodes.EdgeToPortMissing, "Edge target port is required.", edgeIndex: index, field: "Edges[" + index + "].ToPort");
                }

                NodeDefinition fromNode;
                var hasFromNode = !string.IsNullOrWhiteSpace(edge.FromNodeId) && nodeMap.TryGetValue(edge.FromNodeId, out fromNode);
                if (!hasFromNode && !string.IsNullOrWhiteSpace(edge.FromNodeId))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EdgeSourceMissing,
                        "Edge source node does not exist: " + edge.FromNodeId,
                        edgeIndex: index,
                        field: "Edges[" + index + "].FromNodeId");
                }

                NodeDefinition toNode;
                var hasToNode = !string.IsNullOrWhiteSpace(edge.ToNodeId) && nodeMap.TryGetValue(edge.ToNodeId, out toNode);
                if (!hasToNode && !string.IsNullOrWhiteSpace(edge.ToNodeId))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EdgeTargetMissing,
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
                            FlowValidationIssueCodes.EdgeSourcePortUnknown,
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
                            FlowValidationIssueCodes.EdgeToPortUnknown,
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
                result.AddError(FlowValidationIssueCodes.EntriesMissing, "Runtime flow must contain at least one entry.", field: "Entries");
                return;
            }

            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    result.AddError(FlowValidationIssueCodes.EntryMissing, "Entry definition must not be null.", field: "Entries[" + index + "]");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.EntryName))
                {
                    result.AddError(FlowValidationIssueCodes.EntryNameMissing, "Entry name is required.", field: "Entries[" + index + "].EntryName");
                }
                else if (!entryNames.Add(entry.EntryName))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryNameDuplicate,
                        "Entry name must be unique: " + entry.EntryName,
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].EntryName");
                }

                if (string.IsNullOrWhiteSpace(entry.TargetNodeId))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryTargetMissing,
                        "Entry target node id is required.",
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].TargetNodeId");
                }
                else if (!nodeMap.ContainsKey(entry.TargetNodeId))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryTargetMissing,
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
                            FlowValidationIssueCodes.RequiredSettingMissing,
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
