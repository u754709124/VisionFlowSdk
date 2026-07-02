using System;
using System.Collections.Generic;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
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
    }
}
