using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner : IFlowRunner, IFlowContinuationDispatcher
    {
        private readonly object _gate = new object();
        private readonly RuntimeFlowDefinition _definition;
        private readonly RuntimeFlowPlan _plan;
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;
        private readonly FlowExecutionOptions _options;
        private readonly Dictionary<string, IFlowNode> _nodeInstances;
        private readonly List<IFlowListenerNode> _startedListeners;
        private CancellationTokenSource _runnerCancellation;

        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink = null)
            : this(definition, nodeRegistry, eventSink, null)
        {
        }

        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices)
            : this(definition, nodeRegistry, eventSink, devices, null)
        {
        }

        public FlowRunner(
            RuntimeFlowDefinition definition,
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            FlowExecutionOptions options)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            if (nodeRegistry == null)
            {
                throw new ArgumentNullException("nodeRegistry");
            }

            _definition = definition;
            _plan = new RuntimeFlowPlan(definition);
            _nodeRegistry = nodeRegistry;
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
            _devices = devices ?? EmptyDeviceRegistry.Instance;
            _options = CloneOptions(options);
            _nodeInstances = new Dictionary<string, IFlowNode>(StringComparer.OrdinalIgnoreCase);
            _startedListeners = new List<IFlowListenerNode>();
        }

        public RuntimeFlowDefinition Definition
        {
            get { return _definition; }
        }

        public bool IsRunning { get; private set; }

        public FlowExecutionOptions Options
        {
            get { return _options; }
        }

        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CancellationToken runnerToken;
            lock (_gate)
            {
                if (IsRunning)
                {
                    return;
                }

                _runnerCancellation = new CancellationTokenSource();
                IsRunning = true;
                runnerToken = _runnerCancellation.Token;
            }

            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
            {
                try
                {
                    await StartListenerNodesAsync(linkedCancellation.Token).ConfigureAwait(false);
                    await PublishAsync(
                        FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStarted, _definition, null),
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await StopStartedListenersAsync(CancellationToken.None).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (_runnerCancellation != null)
                        {
                            _runnerCancellation.Cancel();
                            _runnerCancellation.Dispose();
                            _runnerCancellation = null;
                        }

                        IsRunning = false;
                    }

                    throw;
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CancellationTokenSource cancellationSource = null;
            lock (_gate)
            {
                if (!IsRunning)
                {
                    return;
                }

                cancellationSource = _runnerCancellation;
                _runnerCancellation = null;
                IsRunning = false;
            }

            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
                cancellationSource.Dispose();
            }

            await StopStartedListenersAsync(cancellationToken).ConfigureAwait(false);
            await PublishAsync(
                FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStopped, _definition, null, null, NodeRuntimeState.Stopped),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
