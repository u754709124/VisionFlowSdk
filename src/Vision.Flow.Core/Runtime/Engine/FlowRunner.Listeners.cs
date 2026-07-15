using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner
    {
        private async Task StartListenerNodesAsync(CancellationToken cancellationToken)
        {
            var startedNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < _definition.Entries.Count; index++)
            {
                var entry = _definition.Entries[index];
                if (entry == null || entry.TriggerKind != FlowTriggerKind.NodeEvent)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.SourceNodeId))
                {
                    throw new InvalidOperationException("NodeEvent entry SourceNodeId is required: " + entry.EntryName);
                }

                if (!startedNodeIds.Add(entry.SourceNodeId))
                {
                    throw new InvalidOperationException("A listener node can only be bound to one NodeEvent entry: " + entry.SourceNodeId);
                }

                var node = FindNode(entry.SourceNodeId);
                var listener = GetOrCreateNode(node) as IFlowListenerNode;
                if (listener == null)
                {
                    throw new InvalidOperationException("NodeEvent entry source must implement IFlowListenerNode: " + entry.SourceNodeId);
                }

                var dispatcher = new BoundFlowContinuationDispatcher(this, entry.EntryName, null, null, null, null);
                await listener.StartAsync(
                    new FlowListenerContext(_definition, node, entry, _devices, _eventSink, dispatcher),
                    cancellationToken).ConfigureAwait(false);

                lock (_gate)
                {
                    if (!_startedListeners.Contains(listener))
                    {
                        _startedListeners.Add(listener);
                    }
                }

                await PublishAsync(
                    CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeWaiting,
                        null,
                        node,
                        NodeRuntimeState.Waiting,
                        null,
                        null,
                        null,
                        0),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task StopStartedListenersAsync(CancellationToken cancellationToken)
        {
            List<IFlowListenerNode> listeners;
            lock (_gate)
            {
                listeners = new List<IFlowListenerNode>(_startedListeners);
                _startedListeners.Clear();
            }

            for (var index = listeners.Count - 1; index >= 0; index--)
            {
                await listeners[index].StopAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
