using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 相机参数设置节点配置，描述目标相机、参数名和写入值。
    /// </summary>
    public sealed class CameraParameterSetNodeConfig
    {
        /// <summary>
        /// 相机 Adapter 标识，对应运行时设备注册表中的相机 ID。
        /// </summary>
        public string CameraId { get; set; }

        /// <summary>
        /// 需要写入的相机参数名，通常来自 Adapter 暴露的参数描述。
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// 参数写入值，运行时会根据参数描述中的 ValueType 做基础类型转换。
        /// </summary>
        public object Value { get; set; }
    }

    public sealed class CameraParameterSetNodeFactory : BaseNodeFactory<CameraParameterSetNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.CameraParameterSet;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return CameraParameterSetNodeDescriptor.Create(); }
        }

        protected override CameraParameterSetNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new CameraParameterSetNodeConfig
            {
                CameraId = GetStringSetting(definition, FlowSettingNames.CameraId, null),
                ParameterName = GetStringSetting(definition, FlowSettingNames.ParameterName, null),
                Value = GetSetting(definition, FlowSettingNames.Value, null)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, CameraParameterSetNodeConfig config)
        {
            return new CameraParameterSetNode(config);
        }
    }

    public sealed class CameraParameterSetNode : IFlowNode
    {
        private readonly CameraParameterSetNodeConfig _config;

        public CameraParameterSetNode(CameraParameterSetNodeConfig config)
        {
            _config = config ?? new CameraParameterSetNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            var cameraId = ResolveString(context, FlowSettingNames.CameraId, _config.CameraId);
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return NodeExecutionResult.Failure("CameraId is required.");
            }

            var parameterName = ResolveString(context, FlowSettingNames.ParameterName, _config.ParameterName);
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                return NodeExecutionResult.Failure("ParameterName is required.");
            }

            var camera = context.Devices.GetCamera(cameraId);
            var descriptor = FindParameter(camera, parameterName);
            if (descriptor == null)
            {
                return NodeExecutionResult.Failure("Camera parameter was not found: " + parameterName);
            }

            if (!descriptor.IsWritable)
            {
                return NodeExecutionResult.Failure("Camera parameter is read-only: " + parameterName);
            }

            var rawValue = context.GetInputValue(FlowSettingNames.Value);
            if (rawValue == null)
            {
                rawValue = _config.Value;
            }

            var converted = CameraNodeHelpers.ConvertParameterValue(rawValue, descriptor.ValueType);
            await camera.SetParameterAsync(parameterName, converted, cancellationToken).ConfigureAwait(false);

            return NodeExecutionResult.Success(
                FlowPortNames.Next,
                new Dictionary<string, object>
                {
                    { FlowOutputNames.Result, true },
                    { FlowOutputNames.VariableName, parameterName },
                    { FlowOutputNames.Value, converted }
                });
        }

        private static CameraParameterDescriptor FindParameter(ICameraAdapter camera, string parameterName)
        {
            var descriptors = camera.GetParameterDescriptors();
            if (descriptors == null)
            {
                return null;
            }

            return descriptors.FirstOrDefault(x =>
                x != null &&
                string.Equals(x.ParameterName, parameterName, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveString(FlowExecutionContext context, string name, string fallback)
        {
            var value = context.GetInputValue(name);
            return value == null ? fallback : Convert.ToString(value);
        }
    }

    public static class CameraParameterSetNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = CameraParameterSetNodeFactory.TypeName,
                DisplayName = "Set Camera Parameter",
                Category = "Camera",
                Version = "1.0.0",
                Description = "Set one writable camera parameter through ICameraAdapter.",
                InputPorts =
                {
                    new NodePortDescriptor { Name = FlowPortNames.In, DisplayName = "In", Direction = FlowPortDirection.Input, DataType = FlowDataType.Control }
                },
                OutputPorts =
                {
                    new NodePortDescriptor { Name = FlowPortNames.Next, DisplayName = "Next", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control },
                    new NodePortDescriptor { Name = FlowPortNames.Error, DisplayName = "Error", Direction = FlowPortDirection.Output, DataType = FlowDataType.Control }
                },
                Settings =
                {
                    new NodeSettingDescriptor { Name = FlowSettingNames.CameraId, DisplayName = "Camera", DataType = FlowDataType.String, IsRequired = true },
                    new NodeSettingDescriptor { Name = FlowSettingNames.ParameterName, DisplayName = "Parameter", DataType = FlowDataType.String, IsRequired = true },
                    new NodeSettingDescriptor { Name = FlowSettingNames.Value, DisplayName = "Value", DataType = FlowDataType.Object, IsRequired = true }
                },
                Outputs =
                {
                    new NodeOutputDescriptor { Name = FlowOutputNames.Result, DisplayName = "Result", DataType = FlowDataType.Boolean },
                    new NodeOutputDescriptor { Name = FlowOutputNames.VariableName, DisplayName = "Parameter Name", DataType = FlowDataType.String },
                    new NodeOutputDescriptor { Name = FlowOutputNames.Value, DisplayName = "Value", DataType = FlowDataType.Object }
                }
            };
        }
    }
}
