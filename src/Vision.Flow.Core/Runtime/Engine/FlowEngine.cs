using System;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.Queues;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed class FlowEngine
    {
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;
        private readonly ICameraFrameRouter _cameraFrames;
        private readonly IFlowTaskQueueRegistry _queues;
        private readonly FlowExecutionOptions _options;

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink = null)
            : this(nodeRegistry, eventSink, null, null)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IDeviceRegistry devices)
            : this(nodeRegistry, null, devices, null)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices)
            : this(nodeRegistry, eventSink, devices, null)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices, ICameraFrameRouter cameraFrames)
            : this(nodeRegistry, eventSink, devices, cameraFrames, null)
        {
        }

        public FlowEngine(
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues)
            : this(nodeRegistry, eventSink, devices, cameraFrames, queues, null)
        {
        }

        public FlowEngine(
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues,
            FlowExecutionOptions options)
        {
            if (nodeRegistry == null)
            {
                throw new ArgumentNullException("nodeRegistry");
            }

            _nodeRegistry = nodeRegistry;
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
            _devices = devices ?? EmptyDeviceRegistry.Instance;
            _cameraFrames = cameraFrames ?? new DefaultCameraFrameRouter();
            _queues = queues ?? new FlowTaskQueueRegistry(_eventSink);
            _options = CloneOptions(options);
        }

        public IFlowRunner CreateRunner(RuntimeFlowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            return new FlowRunner(definition, _nodeRegistry, _eventSink, _devices, _cameraFrames, _queues, _options);
        }

        private static FlowExecutionOptions CloneOptions(FlowExecutionOptions options)
        {
            var source = options ?? new FlowExecutionOptions();
            return new FlowExecutionOptions
            {
                FanOutMode = source.FanOutMode,
                MaxDegreeOfParallelism = source.MaxDegreeOfParallelism <= 0 ? 1 : source.MaxDegreeOfParallelism,
                BranchTokenMode = source.BranchTokenMode,
                ContinueOnBranchFailure = source.ContinueOnBranchFailure,
                DefaultNodeTimeoutMs = source.DefaultNodeTimeoutMs
            };
        }
    }
}
