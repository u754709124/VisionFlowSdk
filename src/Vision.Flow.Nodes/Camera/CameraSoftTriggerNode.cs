using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // 软触发节点创建触发上下文，不等待图像回调完成。
    public sealed class CameraSoftTriggerNodeConfig
    {
        public CameraSoftTriggerNodeConfig()
        {
            TimeoutMs = 1000;
        }

        public string CameraId { get; set; }

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
                CameraId = GetStringSetting(definition, "CameraId", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 1000)
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
            cancellationToken.ThrowIfCancellationRequested();

            var cameraId = CameraNodeHelpers.ResolveString(context, "CameraId", _config.CameraId);
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return NodeExecutionResult.Failure("CameraId is required.");
            }

            var timeoutMs = CameraNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var camera = context.Devices.GetCamera(cameraId);
            var triggerId = Guid.NewGuid().ToString("N");
            var triggerTime = DateTime.UtcNow;
            var triggerContext = new CameraTriggerContext
            {
                CameraId = cameraId,
                TriggerId = triggerId,
                Token = context.Token
            };
            triggerContext.Metadata[FlowMetadataKeys.CameraId] = cameraId;
            triggerContext.Metadata[FlowMetadataKeys.TriggerId] = triggerId;
            triggerContext.Metadata[FlowMetadataKeys.TriggerTime] = triggerTime;

            context.CameraFrames.EnsureCamera(camera, cameraId);

            using (var timeout = CameraNodeTimeout.Create(timeoutMs, cancellationToken))
            {
                try
                {
                    await camera.SoftTriggerAsync(triggerContext, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    return NodeExecutionResult.Timeout("Timed out executing camera soft trigger.", "Timeout");
                }
            }

            context.Token.Set("CameraId", cameraId);
            context.Token.Set("TriggerId", triggerId);
            context.Token.Set("TriggerTime", triggerTime);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "CameraId", cameraId },
                    { "TriggerId", triggerId },
                    { "TriggerTime", triggerTime }
                });
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
                Description = "Issues a software trigger through a camera adapter without waiting for an image.",
                InputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "In",
                        DisplayName = "In",
                        Direction = "Input",
                        DataType = "Control",
                        IsRequired = true,
                        Description = "Execution input."
                    }
                },
                OutputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "Next",
                        DisplayName = "Next",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Continues immediately after the soft trigger is accepted."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Timeout",
                        DisplayName = "Timeout",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes device operation timeout."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Error",
                        DisplayName = "Error",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes invalid configuration or adapter errors."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered camera adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TimeoutMs",
                        DisplayName = "Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 1000,
                        IsRequired = true,
                        Description = "Maximum time for the trigger command. Zero disables the node timeout."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        Description = "The resolved camera id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "TriggerId",
                        DisplayName = "Trigger Id",
                        DataType = "String",
                        Description = "Generated trigger id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "TriggerTime",
                        DisplayName = "Trigger Time",
                        DataType = "DateTime",
                        Description = "UTC time when the trigger command was issued."
                    }
                }
            };
        }
    }
}
