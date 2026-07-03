using System;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Nodes
{
    /// <summary>
    /// 相机硬触发回调节点配置，描述流程启动后要订阅的相机。
    /// </summary>
    public sealed class CameraHardTriggerNodeConfig
    {
        /// <summary>
        /// 相机 Adapter 标识，对应运行时设备注册表中的相机 ID。
        /// </summary>
        public string CameraId { get; set; }
    }

    public sealed class CameraHardTriggerNodeFactory : BaseNodeFactory<CameraHardTriggerNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.CameraHardTrigger;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return CameraHardTriggerNodeDescriptor.Create(); }
        }

        protected override CameraHardTriggerNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new CameraHardTriggerNodeConfig
            {
                CameraId = GetStringSetting(definition, FlowSettingNames.CameraId, null)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, CameraHardTriggerNodeConfig config)
        {
            return new CameraHardTriggerNode(config);
        }
    }

    public sealed class CameraHardTriggerNode : IFlowListenerNode
    {
        private readonly object _gate = new object();
        private readonly CameraHardTriggerNodeConfig _config;
        private FlowListenerContext _context;
        private ICameraAdapter _camera;
        private CancellationTokenSource _listenerCancellation;
        private bool _isStarted;

        public CameraHardTriggerNode(CameraHardTriggerNodeConfig config)
        {
            _config = config ?? new CameraHardTriggerNodeConfig();
        }

        public Task StartAsync(FlowListenerContext context, CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var cameraId = ResolveCameraId(context.Node);
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                throw new InvalidOperationException("CameraId is required.");
            }

            var camera = context.Devices.GetCamera(cameraId);
            lock (_gate)
            {
                if (_isStarted)
                {
                    return Task.FromResult(0);
                }

                cancellationToken.ThrowIfCancellationRequested();
                _context = context;
                _camera = camera;
                _listenerCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _camera.FrameArrived += OnFrameArrived;
                _isStarted = true;
            }

            return Task.FromResult(0);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ICameraAdapter camera = null;
            CancellationTokenSource listenerCancellation = null;
            lock (_gate)
            {
                if (!_isStarted)
                {
                    return Task.FromResult(0);
                }

                camera = _camera;
                listenerCancellation = _listenerCancellation;
                _camera = null;
                _context = null;
                _listenerCancellation = null;
                _isStarted = false;
            }

            if (camera != null)
            {
                camera.FrameArrived -= OnFrameArrived;
            }

            if (listenerCancellation != null)
            {
                listenerCancellation.Cancel();
                listenerCancellation.Dispose();
            }

            return Task.FromResult(0);
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(NodeExecutionResult.Success(FlowPortNames.Waiting));
        }

        private void OnFrameArrived(object sender, CameraFrameArrivedEventArgs args)
        {
            FlowListenerContext context;
            CancellationToken token;
            lock (_gate)
            {
                if (!_isStarted || _context == null || _listenerCancellation == null || _listenerCancellation.IsCancellationRequested)
                {
                    return;
                }

                context = _context;
                token = _listenerCancellation.Token;
            }

            var frame = CameraNodeHelpers.CloneFrame(args == null ? null : args.Frame);
            if (frame == null || token.IsCancellationRequested)
            {
                return;
            }

            Task.Run(
                async delegate
                {
                    await DispatchFrameAsync(context, frame, token).ConfigureAwait(false);
                },
                token);
        }

        private async Task DispatchFrameAsync(FlowListenerContext context, CameraFrameData frame, CancellationToken cancellationToken)
        {
            try
            {
                var token = CreateFrameToken(frame);
                await context.Continuations.DispatchAsync(
                    new FlowContinuation
                    {
                        SourceNodeId = context.Node.Id,
                        OutputPort = FlowPortNames.Frame,
                        Token = token,
                        Variables = new VariablePool(),
                        Outputs = CameraNodeHelpers.CreateFrameOutputs(frame),
                        FlowRunId = Guid.NewGuid().ToString("N")
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                var runtimeEvent = FlowRuntimeEvent.Create(
                    FlowRuntimeEventType.NodeFailed,
                    context.Flow,
                    null,
                    context.Node,
                    NodeRuntimeState.Failed,
                    ex.Message,
                    FlowPortNames.Error);
                await context.Events.PublishAsync(runtimeEvent, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static FlowToken CreateFrameToken(CameraFrameData frame)
        {
            var token = new FlowToken
            {
                TokenId = string.IsNullOrWhiteSpace(frame.TriggerId) ? Guid.NewGuid().ToString("N") : frame.TriggerId,
                FrameId = frame.FrameId
            };
            token.Metadata[FlowMetadataKeys.CameraId] = frame.CameraId;
            token.Metadata[FlowMetadataKeys.TriggerId] = frame.TriggerId;
            token.Metadata[FlowMetadataKeys.FrameId] = frame.FrameId;
            token.Metadata[FlowMetadataKeys.GrabTime] = frame.GrabTime;
            return token;
        }

        private string ResolveCameraId(NodeDefinition node)
        {
            object value;
            if (node != null && node.Settings != null && node.Settings.TryGetValue(FlowSettingNames.CameraId, out value))
            {
                return Convert.ToString(value);
            }

            return _config.CameraId;
        }
    }

    public static class CameraHardTriggerNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = CameraHardTriggerNodeFactory.TypeName,
                DisplayName = "Camera Hard Trigger",
                Category = "Camera",
                Version = "1.0.0",
                Description = "Listen to camera hard-trigger frames and dispatch each frame to downstream nodes.",
                OutputPorts =
                {
                    new NodePortDescriptor { Name = FlowPortNames.Frame, DisplayName = "Frame", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control },
                    new NodePortDescriptor { Name = FlowPortNames.Error, DisplayName = "Error", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control },
                    new NodePortDescriptor { Name = FlowPortNames.Waiting, DisplayName = "Waiting", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control }
                },
                Settings =
                {
                    new NodeSettingDescriptor { Name = FlowSettingNames.CameraId, DisplayName = "Camera", DataType = FlowDataType.String, IsRequired = true }
                },
                Outputs =
                {
                    new NodeOutputDescriptor { Name = FlowOutputNames.Image, DisplayName = "Image", DataType = FlowDataType.IVisionImage },
                    new NodeOutputDescriptor { Name = FlowOutputNames.Frame, DisplayName = "Frame", DataType = FlowDataType.CameraFrameData },
                    new NodeOutputDescriptor { Name = FlowOutputNames.FrameId, DisplayName = "Frame Id", DataType = FlowDataType.String },
                    new NodeOutputDescriptor { Name = FlowOutputNames.GrabTime, DisplayName = "Grab Time", DataType = FlowDataType.DateTime },
                    new NodeOutputDescriptor { Name = FlowOutputNames.Metadata, DisplayName = "Metadata", DataType = FlowDataType.Object },
                    new NodeOutputDescriptor { Name = FlowOutputNames.CameraId, DisplayName = "Camera Id", DataType = FlowDataType.String },
                    new NodeOutputDescriptor { Name = FlowOutputNames.TriggerId, DisplayName = "Trigger Id", DataType = FlowDataType.String }
                }
            };
        }
    }
}
