using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner
    {
        private void EnsureReadyQueueScopeIsExecutable(string sourceNodeId)
        {
            // Continuations publish the already-completed source node before scheduling
            // downstream work. Build and validate the reachable scope first so an invalid
            // graph cannot leave variables or lifecycle events behind.
            new ReadyQueueGraphState(_plan, sourceNodeId);
        }

        private async Task ExecuteReadyQueueAsync(
            string sourceNodeId,
            string completedOutputPort,
            bool sourceAlreadyCompleted,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = new ReadyQueueGraphState(_plan, sourceNodeId);
            IList<NodeDefinition> skippedNodes;
            if (sourceAlreadyCompleted)
            {
                skippedNodes = state.ResolveCompletedSource(completedOutputPort);
            }
            else
            {
                state.EnqueueEntryNode();
                skippedNodes = new List<NodeDefinition>();
            }

            await PublishSkippedNodesAsync(skippedNodes, token, cancellationToken, flowRunId).ConfigureAwait(false);
            if (_options.FanOutMode == FlowFanOutMode.Parallel)
            {
                await ExecuteReadyNodesInParallelAsync(
                    state,
                    token,
                    variables,
                    triggerInputs,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
            }
            else
            {
                await ExecuteReadyNodesSequentiallyAsync(
                    state,
                    token,
                    variables,
                    triggerInputs,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
            }

            state.EnsureTerminal();
        }

        private async Task ExecuteReadyNodesSequentiallyAsync(
            ReadyQueueGraphState state,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            NodeDefinition node;
            while (state.TryTakeReadyNode(out node))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await ExecuteNodeAsync(
                    node,
                    token,
                    variables,
                    triggerInputs,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
                var skippedNodes = state.CompleteNode(node, GetEffectiveOutputPort(result));
                await PublishSkippedNodesAsync(skippedNodes, token, cancellationToken, flowRunId).ConfigureAwait(false);
            }
        }

        private async Task ExecuteReadyNodesInParallelAsync(
            ReadyQueueGraphState state,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            var maxDegree = Math.Max(1, _options.MaxDegreeOfParallelism);
            using (var schedulerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var runningTasks = new List<Task<ScheduledNodeCompletion>>();
                try
                {
                    while (state.HasReadyNodes || runningTasks.Count > 0)
                    {
                        NodeDefinition node;
                        while (runningTasks.Count < maxDegree && state.TryTakeReadyNode(out node))
                        {
                            var scheduledNode = node;
                            runningTasks.Add(Task.Run(
                                async delegate
                                {
                                    var result = await ExecuteNodeAsync(
                                        scheduledNode,
                                        token,
                                        variables,
                                        triggerInputs,
                                        schedulerCancellation.Token,
                                        flowRunId).ConfigureAwait(false);
                                    return new ScheduledNodeCompletion(scheduledNode, result);
                                },
                                schedulerCancellation.Token));
                        }

                        if (runningTasks.Count == 0)
                        {
                            break;
                        }

                        var completedTask = await Task.WhenAny(runningTasks).ConfigureAwait(false);
                        runningTasks.Remove(completedTask);
                        var completion = await completedTask.ConfigureAwait(false);
                        var skippedNodes = state.CompleteNode(
                            completion.Node,
                            GetEffectiveOutputPort(completion.Result));
                        await PublishSkippedNodesAsync(
                            skippedNodes,
                            token,
                            schedulerCancellation.Token,
                            flowRunId).ConfigureAwait(false);
                    }
                }
                catch
                {
                    schedulerCancellation.Cancel();
                    await ObserveScheduledTasksAsync(runningTasks).ConfigureAwait(false);
                    throw;
                }
            }
        }

        private async Task PublishSkippedNodesAsync(
            IList<NodeDefinition> skippedNodes,
            FlowToken token,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            if (skippedNodes == null)
            {
                return;
            }

            for (var index = 0; index < skippedNodes.Count; index++)
            {
                var node = skippedNodes[index];
                await PublishAsync(
                    CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeSkipped,
                        token,
                        node,
                        NodeRuntimeState.Skipped,
                        "All reachable inbound control edges were skipped.",
                        null,
                        flowRunId,
                        0),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task ObserveScheduledTasksAsync(IList<Task<ScheduledNodeCompletion>> tasks)
        {
            if (tasks == null)
            {
                return;
            }

            for (var index = 0; index < tasks.Count; index++)
            {
                try
                {
                    await tasks[index].ConfigureAwait(false);
                }
                catch
                {
                }
            }
        }

        private static string GetEffectiveOutputPort(NodeExecutionResult result)
        {
            return result == null || string.IsNullOrWhiteSpace(result.OutputPort)
                ? FlowPortNames.Next
                : result.OutputPort;
        }

        private sealed class ScheduledNodeCompletion
        {
            public ScheduledNodeCompletion(NodeDefinition node, NodeExecutionResult result)
            {
                Node = node;
                Result = result;
            }

            public NodeDefinition Node { get; private set; }

            public NodeExecutionResult Result { get; private set; }
        }

        private sealed class ReadyQueueGraphState
        {
            private readonly RuntimeFlowPlan _plan;
            private readonly string _sourceNodeId;
            private readonly Dictionary<string, NodeDefinition> _nodes;
            private readonly Dictionary<string, ScheduledNodeState> _nodeStates;
            private readonly Dictionary<EdgeDefinition, ScheduledEdgeState> _edgeStates;
            private readonly Dictionary<string, List<EdgeDefinition>> _incomingEdges;
            private readonly Dictionary<string, List<EdgeDefinition>> _outgoingEdges;
            private readonly Queue<NodeDefinition> _readyNodes;

            public ReadyQueueGraphState(RuntimeFlowPlan plan, string sourceNodeId)
            {
                if (plan == null)
                {
                    throw new ArgumentNullException("plan");
                }

                if (string.IsNullOrWhiteSpace(sourceNodeId))
                {
                    throw new ArgumentException("Source node id is required.", "sourceNodeId");
                }

                _plan = plan;
                _sourceNodeId = sourceNodeId;
                _nodes = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);
                _nodeStates = new Dictionary<string, ScheduledNodeState>(StringComparer.OrdinalIgnoreCase);
                _edgeStates = new Dictionary<EdgeDefinition, ScheduledEdgeState>();
                _incomingEdges = new Dictionary<string, List<EdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
                _outgoingEdges = new Dictionary<string, List<EdgeDefinition>>(StringComparer.OrdinalIgnoreCase);
                _readyNodes = new Queue<NodeDefinition>();

                BuildReachableScope();
                EnsureAcyclic();
            }

            public bool HasReadyNodes
            {
                get { return _readyNodes.Count > 0; }
            }

            public void EnqueueEntryNode()
            {
                NodeDefinition source;
                if (!_nodes.TryGetValue(_sourceNodeId, out source))
                {
                    throw new InvalidOperationException("Flow node was not found: " + _sourceNodeId);
                }

                _nodeStates[source.Id] = ScheduledNodeState.Ready;
                _readyNodes.Enqueue(source);
            }

            public IList<NodeDefinition> ResolveCompletedSource(string outputPort)
            {
                NodeDefinition source;
                if (!_nodes.TryGetValue(_sourceNodeId, out source))
                {
                    throw new InvalidOperationException("Flow node was not found: " + _sourceNodeId);
                }

                _nodeStates[source.Id] = ScheduledNodeState.Completed;
                return ResolveOutgoingEdges(source.Id, outputPort);
            }

            public bool TryTakeReadyNode(out NodeDefinition node)
            {
                node = null;
                while (_readyNodes.Count > 0)
                {
                    var candidate = _readyNodes.Dequeue();
                    ScheduledNodeState state;
                    if (!_nodeStates.TryGetValue(candidate.Id, out state) || state != ScheduledNodeState.Ready)
                    {
                        continue;
                    }

                    _nodeStates[candidate.Id] = ScheduledNodeState.Running;
                    node = candidate;
                    return true;
                }

                return false;
            }

            public IList<NodeDefinition> CompleteNode(NodeDefinition node, string outputPort)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    throw new ArgumentNullException("node");
                }

                _nodeStates[node.Id] = ScheduledNodeState.Completed;
                return ResolveOutgoingEdges(node.Id, outputPort);
            }

            public void EnsureTerminal()
            {
                foreach (var item in _nodeStates)
                {
                    if (item.Value == ScheduledNodeState.Pending ||
                        item.Value == ScheduledNodeState.Ready ||
                        item.Value == ScheduledNodeState.Running)
                    {
                        throw new InvalidOperationException(
                            "Graph scheduling stalled before node reached a terminal state: " + item.Key);
                    }
                }
            }

            private void BuildReachableScope()
            {
                NodeDefinition source;
                if (!_plan.NodesById.TryGetValue(_sourceNodeId, out source) || source == null)
                {
                    throw new InvalidOperationException("Flow node was not found: " + _sourceNodeId);
                }

                AddReachableNode(source);
                var pendingNodes = new Queue<NodeDefinition>();
                pendingNodes.Enqueue(source);
                while (pendingNodes.Count > 0)
                {
                    var current = pendingNodes.Dequeue();
                    var outgoing = _plan.GetOutgoingEdges(current.Id);
                    for (var index = 0; index < outgoing.Count; index++)
                    {
                        var edge = outgoing[index];
                        if (edge == null)
                        {
                            continue;
                        }

                        NodeDefinition target;
                        if (string.IsNullOrWhiteSpace(edge.ToNodeId) ||
                            !_plan.NodesById.TryGetValue(edge.ToNodeId, out target) ||
                            target == null)
                        {
                            throw new InvalidOperationException(
                                "Flow edge target node was not found: " + edge.ToNodeId);
                        }

                        if (!_edgeStates.ContainsKey(edge))
                        {
                            _edgeStates[edge] = ScheduledEdgeState.Unknown;
                            GetOrCreateEdges(_outgoingEdges, current.Id).Add(edge);
                            GetOrCreateEdges(_incomingEdges, target.Id).Add(edge);
                        }

                        if (AddReachableNode(target))
                        {
                            pendingNodes.Enqueue(target);
                        }
                    }
                }
            }

            private bool AddReachableNode(NodeDefinition node)
            {
                if (_nodes.ContainsKey(node.Id))
                {
                    return false;
                }

                _nodes[node.Id] = node;
                _nodeStates[node.Id] = ScheduledNodeState.Pending;
                return true;
            }

            private IList<NodeDefinition> ResolveOutgoingEdges(string nodeId, string selectedOutputPort)
            {
                var skippedNodes = new List<NodeDefinition>();
                var candidates = new Queue<string>();
                List<EdgeDefinition> outgoing;
                if (_outgoingEdges.TryGetValue(nodeId, out outgoing))
                {
                    var effectiveOutputPort = string.IsNullOrWhiteSpace(selectedOutputPort)
                        ? FlowPortNames.Next
                        : selectedOutputPort;
                    for (var index = 0; index < outgoing.Count; index++)
                    {
                        var edge = outgoing[index];
                        if (_edgeStates[edge] != ScheduledEdgeState.Unknown)
                        {
                            continue;
                        }

                        var edgePort = string.IsNullOrWhiteSpace(edge.FromPort)
                            ? FlowPortNames.Next
                            : edge.FromPort;
                        _edgeStates[edge] = string.Equals(
                            edgePort,
                            effectiveOutputPort,
                            StringComparison.OrdinalIgnoreCase)
                            ? ScheduledEdgeState.Taken
                            : ScheduledEdgeState.Skipped;
                        candidates.Enqueue(edge.ToNodeId);
                    }
                }

                EvaluateCandidates(candidates, skippedNodes);
                return skippedNodes;
            }

            private void EvaluateCandidates(Queue<string> candidates, IList<NodeDefinition> skippedNodes)
            {
                while (candidates.Count > 0)
                {
                    var nodeId = candidates.Dequeue();
                    ScheduledNodeState nodeState;
                    if (!_nodeStates.TryGetValue(nodeId, out nodeState) || nodeState != ScheduledNodeState.Pending)
                    {
                        continue;
                    }

                    List<EdgeDefinition> incoming;
                    if (!_incomingEdges.TryGetValue(nodeId, out incoming) || incoming.Count == 0)
                    {
                        continue;
                    }

                    var hasUnknown = false;
                    var hasTaken = false;
                    for (var index = 0; index < incoming.Count; index++)
                    {
                        var edgeState = _edgeStates[incoming[index]];
                        if (edgeState == ScheduledEdgeState.Unknown)
                        {
                            hasUnknown = true;
                            break;
                        }

                        if (edgeState == ScheduledEdgeState.Taken)
                        {
                            hasTaken = true;
                        }
                    }

                    if (hasUnknown)
                    {
                        continue;
                    }

                    NodeDefinition node;
                    if (!_nodes.TryGetValue(nodeId, out node))
                    {
                        continue;
                    }

                    if (hasTaken)
                    {
                        _nodeStates[nodeId] = ScheduledNodeState.Ready;
                        _readyNodes.Enqueue(node);
                        continue;
                    }

                    _nodeStates[nodeId] = ScheduledNodeState.Skipped;
                    skippedNodes.Add(node);
                    List<EdgeDefinition> outgoing;
                    if (!_outgoingEdges.TryGetValue(nodeId, out outgoing))
                    {
                        continue;
                    }

                    for (var index = 0; index < outgoing.Count; index++)
                    {
                        var edge = outgoing[index];
                        if (_edgeStates[edge] == ScheduledEdgeState.Unknown)
                        {
                            _edgeStates[edge] = ScheduledEdgeState.Skipped;
                            candidates.Enqueue(edge.ToNodeId);
                        }
                    }
                }
            }

            private void EnsureAcyclic()
            {
                var indegrees = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var nodeId in _nodes.Keys)
                {
                    List<EdgeDefinition> incoming;
                    indegrees[nodeId] = _incomingEdges.TryGetValue(nodeId, out incoming)
                        ? incoming.Count
                        : 0;
                }

                var ready = new Queue<string>(indegrees.Where(x => x.Value == 0).Select(x => x.Key));
                var visited = 0;
                while (ready.Count > 0)
                {
                    var nodeId = ready.Dequeue();
                    visited++;
                    List<EdgeDefinition> outgoing;
                    if (!_outgoingEdges.TryGetValue(nodeId, out outgoing))
                    {
                        continue;
                    }

                    for (var index = 0; index < outgoing.Count; index++)
                    {
                        var targetNodeId = outgoing[index].ToNodeId;
                        indegrees[targetNodeId]--;
                        if (indegrees[targetNodeId] == 0)
                        {
                            ready.Enqueue(targetNodeId);
                        }
                    }
                }

                if (visited == _nodes.Count)
                {
                    return;
                }

                var cycleNodeId = indegrees.First(x => x.Value > 0).Key;
                throw new InvalidOperationException("Cycle detected while executing node: " + cycleNodeId);
            }

            private static List<EdgeDefinition> GetOrCreateEdges(
                IDictionary<string, List<EdgeDefinition>> edgesByNode,
                string nodeId)
            {
                List<EdgeDefinition> edges;
                if (!edgesByNode.TryGetValue(nodeId, out edges))
                {
                    edges = new List<EdgeDefinition>();
                    edgesByNode[nodeId] = edges;
                }

                return edges;
            }
        }

        private enum ScheduledNodeState
        {
            Pending = 0,
            Ready = 1,
            Running = 2,
            Completed = 3,
            Skipped = 4
        }

        private enum ScheduledEdgeState
        {
            Unknown = 0,
            Taken = 1,
            Skipped = 2
        }
    }
}
