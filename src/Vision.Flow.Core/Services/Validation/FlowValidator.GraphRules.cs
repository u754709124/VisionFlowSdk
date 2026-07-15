using System;
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

        private static void ValidateNoCycles(
            IList<EdgeDefinition> edges,
            IDictionary<string, NodeDefinition> nodeMap,
            FlowValidationResult result)
        {
            var adjacency = new Dictionary<string, List<CycleEdge>>(StringComparer.OrdinalIgnoreCase);
            foreach (var nodeId in nodeMap.Keys)
            {
                adjacency[nodeId] = new List<CycleEdge>();
            }

            for (var index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];
                if (edge == null ||
                    string.IsNullOrWhiteSpace(edge.FromNodeId) ||
                    string.IsNullOrWhiteSpace(edge.ToNodeId) ||
                    string.IsNullOrWhiteSpace(edge.FromPort) ||
                    string.IsNullOrWhiteSpace(edge.ToPort) ||
                    !nodeMap.ContainsKey(edge.FromNodeId) ||
                    !nodeMap.ContainsKey(edge.ToNodeId))
                {
                    continue;
                }

                adjacency[edge.FromNodeId].Add(new CycleEdge(edge.ToNodeId, index));
            }

            var states = new Dictionary<string, CycleVisitState>(StringComparer.OrdinalIgnoreCase);
            var path = new List<string>();
            foreach (var nodeId in nodeMap.Keys)
            {
                CycleVisitState state;
                if (!states.TryGetValue(nodeId, out state) || state == CycleVisitState.Unvisited)
                {
                    VisitForCycleValidation(nodeId, adjacency, states, path, result);
                }
            }
        }

        private static void VisitForCycleValidation(
            string nodeId,
            IDictionary<string, List<CycleEdge>> adjacency,
            IDictionary<string, CycleVisitState> states,
            IList<string> path,
            FlowValidationResult result)
        {
            states[nodeId] = CycleVisitState.Visiting;
            path.Add(nodeId);

            List<CycleEdge> outgoing;
            if (adjacency.TryGetValue(nodeId, out outgoing))
            {
                for (var index = 0; index < outgoing.Count; index++)
                {
                    var edge = outgoing[index];
                    CycleVisitState targetState;
                    if (!states.TryGetValue(edge.TargetNodeId, out targetState))
                    {
                        targetState = CycleVisitState.Unvisited;
                    }

                    if (targetState == CycleVisitState.Unvisited)
                    {
                        VisitForCycleValidation(edge.TargetNodeId, adjacency, states, path, result);
                    }
                    else if (targetState == CycleVisitState.Visiting)
                    {
                        result.AddError(
                            FlowValidationIssueCodes.FlowCycleDetected,
                            "Directed flow cycle detected: " + FormatCyclePath(path, edge.TargetNodeId),
                            nodeId: nodeId,
                            edgeIndex: edge.EdgeIndex,
                            field: "Edges[" + edge.EdgeIndex + "]");
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            states[nodeId] = CycleVisitState.Visited;
        }

        private static string FormatCyclePath(IList<string> path, string targetNodeId)
        {
            var cycleStart = 0;
            for (var index = 0; index < path.Count; index++)
            {
                if (string.Equals(path[index], targetNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    cycleStart = index;
                    break;
                }
            }

            var cycle = new List<string>();
            for (var index = cycleStart; index < path.Count; index++)
            {
                cycle.Add(path[index]);
            }

            cycle.Add(targetNodeId);
            return string.Join(" -> ", cycle);
        }

        private sealed class CycleEdge
        {
            public CycleEdge(string targetNodeId, int edgeIndex)
            {
                TargetNodeId = targetNodeId;
                EdgeIndex = edgeIndex;
            }

            public string TargetNodeId { get; private set; }

            public int EdgeIndex { get; private set; }
        }

        private enum CycleVisitState
        {
            Unvisited,
            Visiting,
            Visited
        }
    }
}
