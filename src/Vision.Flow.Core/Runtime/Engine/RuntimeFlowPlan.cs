using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.Engine
{
    /// <summary>
    /// 保存流程定义的只读运行时索引，并缓存已完成可达性与环检测的执行作用域。
    /// </summary>
    public sealed class RuntimeFlowPlan
    {
        private readonly object _scopeGate = new object();
        private readonly Dictionary<string, Dictionary<string, List<EdgeDefinition>>> _outgoingEdgesByNodeAndPort;
        private readonly Dictionary<string, List<EdgeDefinition>> _outgoingEdgesByNode;
        private readonly Dictionary<string, CompiledGraphScope> _executionScopes;

        /// <summary>
        /// 从不可变运行时定义创建索引，并预编译所有入口可到达的执行作用域。
        /// </summary>
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
            _outgoingEdgesByNode = new Dictionary<string, List<EdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
            _executionScopes = new Dictionary<string, CompiledGraphScope>(StringComparer.OrdinalIgnoreCase);

            BuildNodeIndex(definition.Nodes);
            BuildEntryIndex(definition.Entries);
            BuildEdgeIndexes(definition.Edges);
            PrecompileEntryScopes(definition.Entries);
        }

        /// <summary>
        /// 获取按节点标识建立的运行时节点索引。
        /// </summary>
        public IDictionary<string, NodeDefinition> NodesById { get; private set; }

        /// <summary>
        /// 获取按入口名称建立的运行时入口索引。
        /// </summary>
        public IDictionary<string, FlowEntryDefinition> EntriesByName { get; private set; }

        /// <summary>
        /// 获取按目标节点建立的入边索引。
        /// </summary>
        public IDictionary<string, List<EdgeDefinition>> IncomingEdgesByNode { get; private set; }

        /// <summary>
        /// 获取节点指定输出端口的出边只读视图。
        /// </summary>
        public IList<EdgeDefinition> GetOutgoingEdges(string nodeId, string outputPort)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return new EdgeDefinition[0];
            }

            Dictionary<string, List<EdgeDefinition>> edgesByPort;
            if (!_outgoingEdgesByNodeAndPort.TryGetValue(nodeId, out edgesByPort))
            {
                return new EdgeDefinition[0];
            }

            var effectivePort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Next : outputPort;
            List<EdgeDefinition> edges;
            return edgesByPort.TryGetValue(effectivePort, out edges) ? edges : (IList<EdgeDefinition>)new EdgeDefinition[0];
        }

        /// <summary>
        /// 获取节点的全部出边只读视图。
        /// </summary>
        public IList<EdgeDefinition> GetOutgoingEdges(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return new EdgeDefinition[0];
            }

            List<EdgeDefinition> edges;
            return _outgoingEdgesByNode.TryGetValue(nodeId, out edges) ? edges : (IList<EdgeDefinition>)new EdgeDefinition[0];
        }

        internal CompiledGraphScope GetExecutionScope(string sourceNodeId)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId))
            {
                throw new ArgumentException("Source node id is required.", "sourceNodeId");
            }

            lock (_scopeGate)
            {
                CompiledGraphScope scope;
                if (!_executionScopes.TryGetValue(sourceNodeId, out scope))
                {
                    scope = CompileExecutionScope(sourceNodeId);
                    _executionScopes[sourceNodeId] = scope;
                }

                return scope;
            }
        }

        private void PrecompileEntryScopes(IList<FlowEntryDefinition> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                var sourceNodeId = entry.TriggerKind == FlowTriggerKind.NodeEvent
                    ? entry.SourceNodeId
                    : entry.TargetNodeId;
                if (!string.IsNullOrWhiteSpace(sourceNodeId) && !_executionScopes.ContainsKey(sourceNodeId))
                {
                    _executionScopes[sourceNodeId] = CompileExecutionScope(sourceNodeId);
                }
            }
        }

        private CompiledGraphScope CompileExecutionScope(string sourceNodeId)
        {
            NodeDefinition source;
            if (!NodesById.TryGetValue(sourceNodeId, out source) || source == null)
            {
                throw new InvalidOperationException("Flow node was not found: " + sourceNodeId);
            }

            var nodes = new List<NodeDefinition>();
            var nodeIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var edges = new List<CompiledGraphEdge>();
            var pendingNodes = new Queue<NodeDefinition>();
            nodeIndexes[source.Id] = 0;
            nodes.Add(source);
            pendingNodes.Enqueue(source);

            while (pendingNodes.Count > 0)
            {
                var current = pendingNodes.Dequeue();
                var currentIndex = nodeIndexes[current.Id];
                var outgoing = GetOutgoingEdges(current.Id);
                for (var edgeIndex = 0; edgeIndex < outgoing.Count; edgeIndex++)
                {
                    var edge = outgoing[edgeIndex];
                    if (edge == null)
                    {
                        continue;
                    }

                    NodeDefinition target;
                    if (string.IsNullOrWhiteSpace(edge.ToNodeId) ||
                        !NodesById.TryGetValue(edge.ToNodeId, out target) ||
                        target == null)
                    {
                        throw new InvalidOperationException("Flow edge target node was not found: " + edge.ToNodeId);
                    }

                    int targetIndex;
                    if (!nodeIndexes.TryGetValue(target.Id, out targetIndex))
                    {
                        targetIndex = nodes.Count;
                        nodeIndexes[target.Id] = targetIndex;
                        nodes.Add(target);
                        pendingNodes.Enqueue(target);
                    }

                    edges.Add(new CompiledGraphEdge(
                        currentIndex,
                        targetIndex,
                        string.IsNullOrWhiteSpace(edge.FromPort) ? FlowPortNames.Next : edge.FromPort));
                }
            }

            return new CompiledGraphScope(sourceNodeId, nodes.ToArray(), edges.ToArray());
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
                if (node != null && !string.IsNullOrWhiteSpace(node.Id) && !NodesById.ContainsKey(node.Id))
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
                if (entry != null && !string.IsNullOrWhiteSpace(entry.EntryName) && !EntriesByName.ContainsKey(entry.EntryName))
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

            List<EdgeDefinition> allOutgoingEdges;
            if (!_outgoingEdgesByNode.TryGetValue(edge.FromNodeId, out allOutgoingEdges))
            {
                allOutgoingEdges = new List<EdgeDefinition>();
                _outgoingEdgesByNode[edge.FromNodeId] = allOutgoingEdges;
            }

            allOutgoingEdges.Add(edge);
            Dictionary<string, List<EdgeDefinition>> edgesByPort;
            if (!_outgoingEdgesByNodeAndPort.TryGetValue(edge.FromNodeId, out edgesByPort))
            {
                edgesByPort = new Dictionary<string, List<EdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
                _outgoingEdgesByNodeAndPort[edge.FromNodeId] = edgesByPort;
            }

            var port = string.IsNullOrWhiteSpace(edge.FromPort) ? FlowPortNames.Next : edge.FromPort;
            List<EdgeDefinition> portEdges;
            if (!edgesByPort.TryGetValue(port, out portEdges))
            {
                portEdges = new List<EdgeDefinition>();
                edgesByPort[port] = portEdges;
            }

            portEdges.Add(edge);
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

    internal sealed class CompiledGraphEdge
    {
        internal CompiledGraphEdge(int sourceIndex, int targetIndex, string outputPort)
        {
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            OutputPort = outputPort;
        }

        internal int SourceIndex { get; private set; }

        internal int TargetIndex { get; private set; }

        internal string OutputPort { get; private set; }
    }

    internal sealed class CompiledGraphScope
    {
        private readonly object _poolGate = new object();
        private readonly Stack<CompiledGraphExecutionState> _statePool;
        private readonly int _poolCapacity;

        internal CompiledGraphScope(string sourceNodeId, NodeDefinition[] nodes, CompiledGraphEdge[] edges)
        {
            SourceNodeId = sourceNodeId;
            Nodes = nodes;
            Edges = edges;
            NodeIndexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < nodes.Length; index++)
            {
                NodeIndexes[nodes[index].Id] = index;
            }
            IncomingEdgeIndexes = BuildEdgeIndexes(nodes.Length, edges, false);
            OutgoingEdgeIndexes = BuildEdgeIndexes(nodes.Length, edges, true);
            EnsureAcyclic();
            _statePool = new Stack<CompiledGraphExecutionState>();
            _poolCapacity = Math.Min(64, Math.Max(4, Environment.ProcessorCount * 2));
        }

        internal string SourceNodeId { get; private set; }

        internal NodeDefinition[] Nodes { get; private set; }

        internal CompiledGraphEdge[] Edges { get; private set; }

        internal IDictionary<string, int> NodeIndexes { get; private set; }

        internal int[][] IncomingEdgeIndexes { get; private set; }

        internal int[][] OutgoingEdgeIndexes { get; private set; }

        internal CompiledGraphExecutionState RentState()
        {
            lock (_poolGate)
            {
                if (_statePool.Count > 0)
                {
                    return _statePool.Pop();
                }
            }

            return new CompiledGraphExecutionState(this);
        }

        internal void ReturnState(CompiledGraphExecutionState state)
        {
            if (state == null)
            {
                return;
            }

            state.Reset();
            lock (_poolGate)
            {
                if (_statePool.Count < _poolCapacity)
                {
                    _statePool.Push(state);
                }
            }
        }

        private static int[][] BuildEdgeIndexes(int nodeCount, CompiledGraphEdge[] edges, bool outgoing)
        {
            var lists = new List<int>[nodeCount];
            for (var index = 0; index < edges.Length; index++)
            {
                var nodeIndex = outgoing ? edges[index].SourceIndex : edges[index].TargetIndex;
                var list = lists[nodeIndex] ?? (lists[nodeIndex] = new List<int>());
                list.Add(index);
            }

            var indexes = new int[nodeCount][];
            for (var index = 0; index < nodeCount; index++)
            {
                indexes[index] = lists[index] == null ? new int[0] : lists[index].ToArray();
            }

            return indexes;
        }

        private void EnsureAcyclic()
        {
            var indegrees = new int[Nodes.Length];
            var ready = new Queue<int>();
            for (var index = 0; index < Nodes.Length; index++)
            {
                indegrees[index] = IncomingEdgeIndexes[index].Length;
                if (indegrees[index] == 0)
                {
                    ready.Enqueue(index);
                }
            }

            var visited = 0;
            while (ready.Count > 0)
            {
                var nodeIndex = ready.Dequeue();
                visited++;
                var outgoing = OutgoingEdgeIndexes[nodeIndex];
                for (var index = 0; index < outgoing.Length; index++)
                {
                    var targetIndex = Edges[outgoing[index]].TargetIndex;
                    indegrees[targetIndex]--;
                    if (indegrees[targetIndex] == 0)
                    {
                        ready.Enqueue(targetIndex);
                    }
                }
            }

            if (visited != Nodes.Length)
            {
                for (var index = 0; index < indegrees.Length; index++)
                {
                    if (indegrees[index] > 0)
                    {
                        throw new InvalidOperationException("Cycle detected while compiling node: " + Nodes[index].Id);
                    }
                }
            }
        }
    }
}
