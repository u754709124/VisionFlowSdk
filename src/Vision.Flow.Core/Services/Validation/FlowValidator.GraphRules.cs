using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
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
    }
}
