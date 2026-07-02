using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed class RuntimeFlowPlan
    {
        private readonly Dictionary<string, Dictionary<string, List<EdgeDefinition>>> _outgoingEdgesByNodeAndPort;

        public RuntimeFlowPlan(RuntimeFlowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            NodesById = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);
            EntriesByName = new Dictionary<string, FlowEntryDefinition>(StringComparer.OrdinalIgnoreCase);
            IncomingEdgesByNode = new Dictionary<string, List<EdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
            _outgoingEdgesByNodeAndPort = new Dictionary<string, Dictionary<string, List<EdgeDefinition>>>(StringComparer.OrdinalIgnoreCase);

            BuildNodeIndex(definition.Nodes);
            BuildEntryIndex(definition.Entries);
            BuildEdgeIndexes(definition.Edges);
        }

        public IDictionary<string, NodeDefinition> NodesById { get; private set; }

        public IDictionary<string, FlowEntryDefinition> EntriesByName { get; private set; }

        public IDictionary<string, List<EdgeDefinition>> IncomingEdgesByNode { get; private set; }

        public IList<EdgeDefinition> GetOutgoingEdges(string nodeId, string outputPort)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return new List<EdgeDefinition>();
            }

            Dictionary<string, List<EdgeDefinition>> edgesByPort;
            if (!_outgoingEdgesByNodeAndPort.TryGetValue(nodeId, out edgesByPort))
            {
                return new List<EdgeDefinition>();
            }

            var effectivePort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Next : outputPort;
            List<EdgeDefinition> edges;
            if (!edgesByPort.TryGetValue(effectivePort, out edges))
            {
                return new List<EdgeDefinition>();
            }

            return edges;
        }

        private void BuildNodeIndex(IList<NodeDefinition> nodes)
        {
            if (nodes == null)
            {
                return;
            }

            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                if (!NodesById.ContainsKey(node.Id))
                {
                    NodesById[node.Id] = node;
                }
            }
        }

        private void BuildEntryIndex(IList<FlowEntryDefinition> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.EntryName))
                {
                    continue;
                }

                if (!EntriesByName.ContainsKey(entry.EntryName))
                {
                    EntriesByName[entry.EntryName] = entry;
                }
            }
        }

        private void BuildEdgeIndexes(IList<EdgeDefinition> edges)
        {
            if (edges == null)
            {
                return;
            }

            for (var index = 0; index < edges.Count; index++)
            {
                var edge = edges[index];
                if (edge == null)
                {
                    continue;
                }

                AddOutgoingEdge(edge);
                AddIncomingEdge(edge);
            }
        }

        private void AddOutgoingEdge(EdgeDefinition edge)
        {
            if (string.IsNullOrWhiteSpace(edge.FromNodeId))
            {
                return;
            }

            Dictionary<string, List<EdgeDefinition>> edgesByPort;
            if (!_outgoingEdgesByNodeAndPort.TryGetValue(edge.FromNodeId, out edgesByPort))
            {
                edgesByPort = new Dictionary<string, List<EdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
                _outgoingEdgesByNodeAndPort[edge.FromNodeId] = edgesByPort;
            }

            var port = string.IsNullOrWhiteSpace(edge.FromPort) ? FlowPortNames.Next : edge.FromPort;
            List<EdgeDefinition> edges;
            if (!edgesByPort.TryGetValue(port, out edges))
            {
                edges = new List<EdgeDefinition>();
                edgesByPort[port] = edges;
            }

            edges.Add(edge);
        }

        private void AddIncomingEdge(EdgeDefinition edge)
        {
            if (string.IsNullOrWhiteSpace(edge.ToNodeId))
            {
                return;
            }

            List<EdgeDefinition> edges;
            if (!IncomingEdgesByNode.TryGetValue(edge.ToNodeId, out edges))
            {
                edges = new List<EdgeDefinition>();
                IncomingEdgesByNode[edge.ToNodeId] = edges;
            }

            edges.Add(edge);
        }
    }
}

