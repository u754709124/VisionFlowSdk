using System;
using System.Collections.Generic;
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
        private static readonly IList<NodeDefinition> EmptyNodes = new NodeDefinition[0];

        private void EnsureReadyQueueScopeIsExecutable(string sourceNodeId)
        {
            // 续流会先发布已完成的源节点，再调度下游。这里必须先取得已编译作用域，
            // 确保无效图不会留下变量写入或不完整生命周期事件。
            _plan.GetExecutionScope(sourceNodeId);
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
            var scope = _plan.GetExecutionScope(sourceNodeId);
            var state = scope.RentState();
            try
            {
                IList<NodeDefinition> skippedNodes;
                if (sourceAlreadyCompleted)
                {
                    skippedNodes = state.ResolveCompletedSource(completedOutputPort);
                }
                else
                {
                    state.EnqueueEntryNode();
                    skippedNodes = EmptyNodes;
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
            finally
            {
                scope.ReturnState(state);
            }
        }

        private async Task ExecuteReadyNodesSequentiallyAsync(
            CompiledGraphExecutionState state,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            NodeExecutionFailedException branchFailure = null;
            NodeDefinition node;
            while (state.TryTakeReadyNode(out node))
            {
                cancellationToken.ThrowIfCancellationRequested();
                NodeExecutionResult result = null;
                NodeExecutionFailedException currentFailure = null;
                try
                {
                    result = await ExecuteNodeAsync(
                        node,
                        token,
                        variables,
                        triggerInputs,
                        cancellationToken,
                        flowRunId).ConfigureAwait(false);
                }
                catch (NodeExecutionFailedException ex)
                {
                    currentFailure = ex;
                    if (branchFailure == null)
                    {
                        branchFailure = ex;
                    }
                }

                var skippedNodes = currentFailure == null
                    ? state.CompleteNode(node, GetEffectiveOutputPort(result))
                    : state.FailNode(node);
                await PublishSkippedNodesAsync(skippedNodes, token, cancellationToken, flowRunId).ConfigureAwait(false);
            }

            if (branchFailure != null)
            {
                throw branchFailure;
            }
        }

        private async Task ExecuteReadyNodesInParallelAsync(
            CompiledGraphExecutionState state,
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
                NodeExecutionFailedException branchFailure = null;
                try
                {
                    while (state.HasReadyNodes || runningTasks.Count > 0)
                    {
                        NodeDefinition node;
                        while (runningTasks.Count < maxDegree && state.TryTakeReadyNode(out node))
                        {
                            runningTasks.Add(ExecuteScheduledNodeAsync(
                                node,
                                token,
                                variables,
                                triggerInputs,
                                schedulerCancellation.Token,
                                flowRunId));
                        }

                        if (runningTasks.Count == 0)
                        {
                            break;
                        }

                        var completedTask = await Task.WhenAny(runningTasks).ConfigureAwait(false);
                        runningTasks.Remove(completedTask);
                        var completion = await completedTask.ConfigureAwait(false);
                        if (completion.Failure != null && branchFailure == null)
                        {
                            branchFailure = completion.Failure;
                        }
                        var skippedNodes = completion.Failure == null
                            ? state.CompleteNode(completion.Node, GetEffectiveOutputPort(completion.Result))
                            : state.FailNode(completion.Node);
                        await PublishSkippedNodesAsync(
                            skippedNodes,
                            token,
                            schedulerCancellation.Token,
                            flowRunId).ConfigureAwait(false);
                    }

                    if (branchFailure != null)
                    {
                        throw branchFailure;
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

        private async Task<ScheduledNodeCompletion> ExecuteScheduledNodeAsync(
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            try
            {
                var result = await ExecuteNodeAsync(
                    node,
                    token,
                    variables,
                    triggerInputs,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
                return new ScheduledNodeCompletion(node, result, null);
            }
            catch (NodeExecutionFailedException ex)
            {
                // 策略失败只终止当前控制分支；调度器继续等待兄弟分支，最终再汇总为 FlowRun 失败。
                return new ScheduledNodeCompletion(node, null, ex);
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
            internal ScheduledNodeCompletion(
                NodeDefinition node,
                NodeExecutionResult result,
                NodeExecutionFailedException failure)
            {
                Node = node;
                Result = result;
                Failure = failure;
            }

            internal NodeDefinition Node { get; private set; }

            internal NodeExecutionResult Result { get; private set; }

            internal NodeExecutionFailedException Failure { get; private set; }
        }
    }

    internal sealed class CompiledGraphExecutionState
    {
        private readonly CompiledGraphScope _scope;
        private readonly ScheduledNodeState[] _nodeStates;
        private readonly ScheduledEdgeState[] _edgeStates;
        private readonly Queue<int> _readyNodes;
        private readonly Queue<int> _candidateNodes;
        private readonly List<NodeDefinition> _skippedNodes;

        internal CompiledGraphExecutionState(CompiledGraphScope scope)
        {
            _scope = scope ?? throw new ArgumentNullException("scope");
            _nodeStates = new ScheduledNodeState[scope.Nodes.Length];
            _edgeStates = new ScheduledEdgeState[scope.Edges.Length];
            _readyNodes = new Queue<int>(scope.Nodes.Length);
            _candidateNodes = new Queue<int>(scope.Edges.Length);
            _skippedNodes = new List<NodeDefinition>();
        }

        internal bool HasReadyNodes
        {
            get { return _readyNodes.Count > 0; }
        }

        internal void EnqueueEntryNode()
        {
            _nodeStates[0] = ScheduledNodeState.Ready;
            _readyNodes.Enqueue(0);
        }

        internal IList<NodeDefinition> ResolveCompletedSource(string outputPort)
        {
            _nodeStates[0] = ScheduledNodeState.Completed;
            return ResolveOutgoingEdges(0, outputPort);
        }

        internal bool TryTakeReadyNode(out NodeDefinition node)
        {
            node = null;
            while (_readyNodes.Count > 0)
            {
                var nodeIndex = _readyNodes.Dequeue();
                if (_nodeStates[nodeIndex] != ScheduledNodeState.Ready)
                {
                    continue;
                }

                _nodeStates[nodeIndex] = ScheduledNodeState.Running;
                node = _scope.Nodes[nodeIndex];
                return true;
            }

            return false;
        }

        internal IList<NodeDefinition> CompleteNode(NodeDefinition node, string outputPort)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                throw new ArgumentNullException("node");
            }

            int nodeIndex;
            if (!_scope.NodeIndexes.TryGetValue(node.Id, out nodeIndex))
            {
                throw new InvalidOperationException("Scheduled node is outside the compiled scope: " + node.Id);
            }

            _nodeStates[nodeIndex] = ScheduledNodeState.Completed;
            return ResolveOutgoingEdges(nodeIndex, outputPort);
        }

        internal IList<NodeDefinition> FailNode(NodeDefinition node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                throw new ArgumentNullException("node");
            }

            int nodeIndex;
            if (!_scope.NodeIndexes.TryGetValue(node.Id, out nodeIndex))
            {
                throw new InvalidOperationException("Scheduled node is outside the compiled scope: " + node.Id);
            }

            _nodeStates[nodeIndex] = ScheduledNodeState.Completed;
            return SkipOutgoingEdges(nodeIndex);
        }

        internal void EnsureTerminal()
        {
            for (var index = 0; index < _nodeStates.Length; index++)
            {
                var state = _nodeStates[index];
                if (state == ScheduledNodeState.Pending ||
                    state == ScheduledNodeState.Ready ||
                    state == ScheduledNodeState.Running)
                {
                    throw new InvalidOperationException(
                        "Graph scheduling stalled before node reached a terminal state: " + _scope.Nodes[index].Id);
                }
            }
        }

        internal void Reset()
        {
            Array.Clear(_nodeStates, 0, _nodeStates.Length);
            Array.Clear(_edgeStates, 0, _edgeStates.Length);
            _readyNodes.Clear();
            _candidateNodes.Clear();
            _skippedNodes.Clear();
        }

        private IList<NodeDefinition> ResolveOutgoingEdges(int nodeIndex, string selectedOutputPort)
        {
            _skippedNodes.Clear();
            _candidateNodes.Clear();
            var effectiveOutputPort = string.IsNullOrWhiteSpace(selectedOutputPort)
                ? FlowPortNames.Next
                : selectedOutputPort;
            var outgoing = _scope.OutgoingEdgeIndexes[nodeIndex];
            for (var index = 0; index < outgoing.Length; index++)
            {
                var edgeIndex = outgoing[index];
                if (_edgeStates[edgeIndex] != ScheduledEdgeState.Unknown)
                {
                    continue;
                }

                var edge = _scope.Edges[edgeIndex];
                _edgeStates[edgeIndex] = string.Equals(
                    edge.OutputPort,
                    effectiveOutputPort,
                    StringComparison.OrdinalIgnoreCase)
                    ? ScheduledEdgeState.Taken
                    : ScheduledEdgeState.Skipped;
                _candidateNodes.Enqueue(edge.TargetIndex);
            }

            EvaluateCandidates();
            return _skippedNodes;
        }

        private IList<NodeDefinition> SkipOutgoingEdges(int nodeIndex)
        {
            _skippedNodes.Clear();
            _candidateNodes.Clear();
            var outgoing = _scope.OutgoingEdgeIndexes[nodeIndex];
            for (var index = 0; index < outgoing.Length; index++)
            {
                var edgeIndex = outgoing[index];
                if (_edgeStates[edgeIndex] != ScheduledEdgeState.Unknown)
                {
                    continue;
                }

                _edgeStates[edgeIndex] = ScheduledEdgeState.Skipped;
                _candidateNodes.Enqueue(_scope.Edges[edgeIndex].TargetIndex);
            }

            EvaluateCandidates();
            return _skippedNodes;
        }

        private void EvaluateCandidates()
        {
            while (_candidateNodes.Count > 0)
            {
                var nodeIndex = _candidateNodes.Dequeue();
                if (_nodeStates[nodeIndex] != ScheduledNodeState.Pending)
                {
                    continue;
                }

                var incoming = _scope.IncomingEdgeIndexes[nodeIndex];
                if (incoming.Length == 0)
                {
                    continue;
                }

                var hasUnknown = false;
                var hasTaken = false;
                for (var index = 0; index < incoming.Length; index++)
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

                if (hasTaken)
                {
                    _nodeStates[nodeIndex] = ScheduledNodeState.Ready;
                    _readyNodes.Enqueue(nodeIndex);
                    continue;
                }

                _nodeStates[nodeIndex] = ScheduledNodeState.Skipped;
                _skippedNodes.Add(_scope.Nodes[nodeIndex]);
                var outgoing = _scope.OutgoingEdgeIndexes[nodeIndex];
                for (var index = 0; index < outgoing.Length; index++)
                {
                    var edgeIndex = outgoing[index];
                    if (_edgeStates[edgeIndex] == ScheduledEdgeState.Unknown)
                    {
                        _edgeStates[edgeIndex] = ScheduledEdgeState.Skipped;
                        _candidateNodes.Enqueue(_scope.Edges[edgeIndex].TargetIndex);
                    }
                }
            }
        }
    }

    internal enum ScheduledNodeState
    {
        Pending = 0,
        Ready = 1,
        Running = 2,
        Completed = 3,
        Skipped = 4
    }

    internal enum ScheduledEdgeState
    {
        Unknown = 0,
        Taken = 1,
        Skipped = 2
    }
}
