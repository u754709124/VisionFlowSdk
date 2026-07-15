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
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            await ExecuteReadyQueueAsync(
                nodeId,
                null,
                false,
                token,
                variables,
                triggerInputs,
                cancellationToken,
                flowRunId).ConfigureAwait(false);
        }

        private async Task ExecuteOutgoingEdgesAsync(
            NodeDefinition node,
            string outputPort,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                return;
            }

            await ExecuteReadyQueueAsync(
                node.Id,
                outputPort,
                true,
                token,
                variables,
                triggerInputs,
                cancellationToken,
                flowRunId).ConfigureAwait(false);
        }

        private FlowEntryDefinition FindEntry(string entryName)
        {
            FlowEntryDefinition entry;
            _plan.EntriesByName.TryGetValue(entryName, out entry);
            if (entry == null)
            {
                throw new ArgumentException("Flow entry was not found: " + entryName, "entryName");
            }

            if (entry.TriggerKind != FlowTriggerKind.NodeEvent && string.IsNullOrWhiteSpace(entry.TargetNodeId))
            {
                throw new InvalidOperationException("Flow entry does not have a target node: " + entryName);
            }

            if (entry.TriggerKind == FlowTriggerKind.NodeEvent && string.IsNullOrWhiteSpace(entry.SourceNodeId))
            {
                throw new InvalidOperationException("NodeEvent flow entry does not have a source node: " + entryName);
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
