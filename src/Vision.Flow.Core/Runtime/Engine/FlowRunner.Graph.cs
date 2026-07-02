using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner
    {
        private async Task ExecuteGraphAsync(
            string nodeId,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!currentPath.Add(nodeId))
            {
                throw new InvalidOperationException("Cycle detected while executing node: " + nodeId);
            }

            try
            {
                var node = FindNode(nodeId);
                var result = await ExecuteNodeAsync(node, token, variables, cancellationToken, flowRunId).ConfigureAwait(false);
                var outputPort = string.IsNullOrWhiteSpace(result.OutputPort) ? FlowPortNames.Next : result.OutputPort;
                await ExecuteOutgoingEdgesAsync(node, outputPort, token, variables, cancellationToken, currentPath, flowRunId)
                    .ConfigureAwait(false);
            }
            finally
            {
                currentPath.Remove(nodeId);
            }
        }

        private async Task ExecuteOutgoingEdgesAsync(
            NodeDefinition node,
            string outputPort,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
        {
            var outgoingEdges = _plan.GetOutgoingEdges(node.Id, outputPort);
            if (outgoingEdges.Count == 0)
            {
                return;
            }

            if (_options.FanOutMode != FlowFanOutMode.Parallel || outgoingEdges.Count == 1)
            {
                for (var index = 0; index < outgoingEdges.Count; index++)
                {
                    var edge = outgoingEdges[index];
                    if (edge == null)
                    {
                        continue;
                    }

                    await ExecuteGraphAsync(edge.ToNodeId, token, variables, cancellationToken, currentPath, flowRunId)
                        .ConfigureAwait(false);
                }

                return;
            }

            await ExecuteOutgoingEdgesInParallelAsync(outgoingEdges, token, variables, cancellationToken, currentPath, flowRunId)
                .ConfigureAwait(false);
        }

        private async Task ExecuteOutgoingEdgesInParallelAsync(
            IList<EdgeDefinition> outgoingEdges,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
        {
            var maxDegree = _options.MaxDegreeOfParallelism <= 0 ? outgoingEdges.Count : _options.MaxDegreeOfParallelism;
            if (maxDegree <= 0)
            {
                maxDegree = 1;
            }

            using (var throttle = new SemaphoreSlim(maxDegree, maxDegree))
            {
                var tasks = new List<Task>();
                for (var index = 0; index < outgoingEdges.Count; index++)
                {
                    var edge = outgoingEdges[index];
                    if (edge == null)
                    {
                        continue;
                    }

                    await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var branchPath = new HashSet<string>(currentPath, StringComparer.OrdinalIgnoreCase);
                    tasks.Add(Task.Run(
                        async delegate
                        {
                            try
                            {
                                await ExecuteGraphAsync(edge.ToNodeId, token, variables, cancellationToken, branchPath, flowRunId)
                                    .ConfigureAwait(false);
                            }
                            finally
                            {
                                throttle.Release();
                            }
                        },
                        cancellationToken));
                }

                if (_options.ContinueOnBranchFailure)
                {
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
                else
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
        }

        private FlowEntryDefinition FindEntry(string entryName)
        {
            FlowEntryDefinition entry;
            _plan.EntriesByName.TryGetValue(entryName, out entry);
            if (entry == null)
            {
                throw new ArgumentException("Flow entry was not found: " + entryName, "entryName");
            }

            if (string.IsNullOrWhiteSpace(entry.TargetNodeId))
            {
                throw new InvalidOperationException("Flow entry does not have a target node: " + entryName);
            }

            return entry;
        }

        private NodeDefinition FindNode(string nodeId)
        {
            NodeDefinition node;
            _plan.NodesById.TryGetValue(nodeId, out node);
            if (node == null)
            {
                throw new InvalidOperationException("Flow node was not found: " + nodeId);
            }

            return node;
        }

        private IFlowNode GetOrCreateNode(NodeDefinition node)
        {
            lock (_gate)
            {
                IFlowNode flowNode;
                if (!_nodeInstances.TryGetValue(node.Id, out flowNode))
                {
                    flowNode = _nodeRegistry.CreateNode(node);
                    _nodeInstances[node.Id] = flowNode;
                }

                return flowNode;
            }
        }
    }
}
