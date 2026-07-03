using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    /// <summary>
    /// 相机软触发节点配置，描述要调用的相机和等待单帧返回的超时时间。
    /// </summary>
    public sealed class CameraSoftTriggerNodeConfig
    {
        /// <summary>
        /// 相机 Adapter 标识，对应运行时设备注册表中的相机 ID。
        /// </summary>
        public string CameraId { get; set; }

        /// <summary>
        /// 单次取像等待超时时间，单位毫秒；未配置时使用节点默认值，小于等于 0 时不额外启用节点超时。
        /// </summary>
        public int TimeoutMs { get; set; }
    }

    public sealed class CameraSoftTriggerNodeFactory : BaseNodeFactory<CameraSoftTriggerNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.CameraSoftTrigger;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return CameraSoftTriggerNodeDescriptor.Create(); }
        }

        protected override CameraSoftTriggerNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new CameraSoftTriggerNodeConfig
            {
                CameraId = GetStringSetting(definition, FlowSettingNames.CameraId, null),
                TimeoutMs = GetInt32Setting(definition, FlowSettingNames.TimeoutMs, 5000)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, CameraSoftTriggerNodeConfig config)
        {
            return new CameraSoftTriggerNode(config);
        }
    }

    public sealed class CameraSoftTriggerNode : IFlowNode
    {
        private readonly CameraSoftTriggerNodeConfig _config;

        public CameraSoftTriggerNode(CameraSoftTriggerNodeConfig config)
        {
            _config = config ?? new CameraSoftTriggerNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            var cameraId = Convert.ToString(context.GetInputValue(FlowSettingNames.CameraId)) ?? _config.CameraId;
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return NodeExecutionResult.Failure("CameraId is required.");
            }

            var camera = context.Devices.GetCamera(cameraId);
            var timeoutMs = ResolveTimeout(context);
            CameraFrameData frame;
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                if (timeoutMs > 0)
                {
                    timeoutSource.CancelAfter(timeoutMs);
                }

                try
                {
                    frame = await camera.GrabOneAsync(timeoutSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return NodeExecutionResult.Timeout("Camera grab one timed out.", FlowPortNames.Timeout);
                }
            }

            frame = CameraNodeHelpers.CloneFrame(frame);
            if (frame == null)
            {
                return NodeExecutionResult.Timeout("Camera grab one did not return a frame.", FlowPortNames.Timeout);
            }

            return NodeExecutionResult.Success(FlowPortNames.Next, CameraNodeHelpers.CreateFrameOutputs(frame));
        }

        private int ResolveTimeout(FlowExecutionContext context)
        {
            var value = context.GetInputValue(FlowSettingNames.TimeoutMs);
            if (value == null)
            {
                return _config.TimeoutMs <= 0 ? 5000 : _config.TimeoutMs;
            }

            return Convert.ToInt32(value);
        }
    }

    public static class CameraSoftTriggerNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = CameraSoftTriggerNodeFactory.TypeName,
                DisplayName = "Camera Soft Trigger",
                Category = "Camera",
                Version = "1.0.0",
                Description = "Call ICameraAdapter.GrabOneAsync and output one frame.",
                InputPorts =
                {
                    new NodePortDescriptor { Name = FlowPortNames.In, DisplayName = "In", Direction = FlowPortDirection.Input, DataType = FlowDataType.Control }
                },
                OutputPorts =
                {
                    new NodePortDescriptor { Name = FlowPortNames.Next, DisplayName = "Next", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control },
                    new NodePortDescriptor { Name = FlowPortNames.Timeout, DisplayName = "Timeout", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control },
                    new NodePortDescriptor { Name = FlowPortNames.Error, DisplayName = "Error", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control }
                },
                Settings =
                {
                    new NodeSettingDescriptor { Name = FlowSettingNames.CameraId, DisplayName = "Camera", DataType = FlowDataType.String, IsRequired = true },
                    new NodeSettingDescriptor { Name = FlowSettingNames.TimeoutMs, DisplayName = "Timeout Ms", DataType = FlowDataType.Int32, DefaultValue = 5000, IsRequired = true }
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
