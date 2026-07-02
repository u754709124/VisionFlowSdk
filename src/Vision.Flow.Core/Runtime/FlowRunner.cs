using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Runtime
{
    public sealed partial class FlowRunner : IFlowRunner, IFlowContinuationDispatcher
    {
        private readonly object _gate = new object();
        private readonly RuntimeFlowDefinition _definition;
        private readonly RuntimeFlowPlan _plan;
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;
        private readonly ICameraFrameRouter _cameraFrames;
        private readonly IFlowTaskQueueRegistry _queues;
        private readonly FlowExecutionOptions _options;
        private readonly Dictionary<string, IFlowNode> _nodeInstances;
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
            ICameraFrameRouter cameraFrames)
            : this(definition, nodeRegistry, eventSink, devices, cameraFrames, null)
        {
        }

        public FlowRunner(
            RuntimeFlowDefinition definition,
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues)
            : this(definition, nodeRegistry, eventSink, devices, cameraFrames, queues, null)
        {
        }

        public FlowRunner(
            RuntimeFlowDefinition definition,
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues,
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
            _cameraFrames = cameraFrames ?? new DefaultCameraFrameRouter();
            _queues = queues ?? new FlowTaskQueueRegistry(_eventSink);
            _options = CloneOptions(options);
            _nodeInstances = new Dictionary<string, IFlowNode>(StringComparer.OrdinalIgnoreCase);
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

        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (_gate)
            {
                if (IsRunning)
                {
                    return Task.FromResult(0);
                }

                _runnerCancellation = new CancellationTokenSource();
                IsRunning = true;
            }

            return PublishAsync(
                FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStarted, _definition, null),
                cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CancellationTokenSource cancellationSource = null;
            lock (_gate)
            {
                if (!IsRunning)
                {
                    return Task.FromResult(0);
                }

                cancellationSource = _runnerCancellation;
                _runnerCancellation = null;
                IsRunning = false;
            }

            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
            }

            return PublishAsync(
                FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStopped, _definition, null, null, NodeRuntimeState.Stopped),
                cancellationToken);
        }
    }
}
