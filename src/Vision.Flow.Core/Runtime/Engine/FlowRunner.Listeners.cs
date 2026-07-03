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
            for (var index = 0; index < _definition.Nodes.Count; index++)
            {
                var node = _definition.Nodes[index];
                if (node == null)
                {
                    continue;
                }

                var listener = GetOrCreateNode(node) as IFlowListenerNode;
                if (listener == null)
                {
                    continue;
                }

                await listener.StartAsync(
                    new FlowListenerContext(_definition, node, _devices, _eventSink, this),
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
