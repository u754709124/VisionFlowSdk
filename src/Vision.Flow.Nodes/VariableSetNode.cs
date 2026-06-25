using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    /// <summary>
    /// 变量写入节点配置，描述要写入变量池的名称和值。
    /// </summary>
    public sealed class VariableSetNodeConfig
    {
        public string VariableName { get; set; }

        public object Value { get; set; }
    }

    public sealed class VariableSetNodeFactory : BaseNodeFactory<VariableSetNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.VariableSet;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return VariableSetNodeDescriptor.Create(); }
        }

        protected override VariableSetNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new VariableSetNodeConfig
            {
                VariableName = GetStringSetting(definition, FlowSettingNames.VariableName, null),
                Value = GetSetting(definition, FlowSettingNames.Value, null)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, VariableSetNodeConfig config)
        {
            return new VariableSetNode(config);
        }
    }

    public sealed class VariableSetNode : IFlowNode
    {
        private readonly VariableSetNodeConfig _config;

        public VariableSetNode(VariableSetNodeConfig config)
        {
            _config = config ?? new VariableSetNodeConfig();
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var variableName = ResolveString(context, FlowSettingNames.VariableName, _config.VariableName);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return Task.FromResult(NodeExecutionResult.Failure("VariableName is required."));
            }

            var value = ResolveValue(context);

            context.Variables.Set(variableName, value);
            return Task.FromResult(
                NodeExecutionResult.Success(
                    FlowPortNames.Next,
                    new Dictionary<string, object>
                    {
                        { FlowOutputNames.VariableName, variableName },
                        { FlowOutputNames.Value, value }
                    }));
        }

        private static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = context.GetInputValue(name);
            return value == null ? defaultValue : Convert.ToString(value);
        }

        private object ResolveValue(FlowExecutionContext context)
        {
            if (context.Node.InputBindings != null && context.Node.InputBindings.ContainsKey(FlowSettingNames.Value))
            {
                return context.GetInputValue(FlowSettingNames.Value);
            }

            if (context.Node.Settings != null && context.Node.Settings.ContainsKey(FlowSettingNames.Value))
            {
                return context.GetInputValue(FlowSettingNames.Value);
            }

            return _config.Value;
        }
    }

    public static class VariableSetNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = VariableSetNodeFactory.TypeName,
                DisplayName = "Set Variable",
                Category = "Common",
                Version = "1.0.0",
                Description = "Writes a named value to the current flow variable pool.",
                InputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.In,
                        DisplayName = FlowPortNames.In,
                        Direction = FlowPortDirections.Input,
                        DataType = FlowDataTypes.Control,
                        IsRequired = true,
                        Description = "Execution input."
                    }
                },
                OutputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.Next,
                        DisplayName = FlowPortNames.Next,
                        Direction = FlowPortDirections.Output,
                        DataType = FlowDataTypes.Control,
                        Description = "Continues after writing the variable."
                    },
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.Error,
                        DisplayName = FlowPortNames.Error,
                        Direction = FlowPortDirections.Output,
                        DataType = FlowDataTypes.Control,
                        Description = "Routes missing variable names."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = FlowSettingNames.VariableName,
                        DisplayName = "Variable Name",
                        DataType = FlowDataTypes.String,
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Name of the variable to write."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = FlowSettingNames.Value,
                        DisplayName = "Value",
                        DataType = FlowDataTypes.Object,
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Value to store. This can also be provided by an input binding."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = FlowOutputNames.VariableName,
                        DisplayName = "Variable Name",
                        DataType = FlowDataTypes.String,
                        Description = "The variable name that was written."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = FlowOutputNames.Value,
                        DisplayName = "Value",
                        DataType = FlowDataTypes.Object,
                        Description = "The value that was written."
                    }
                }
            };
        }
    }
}
