using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    public sealed class VariableSetNodeConfig
    {
        public string VariableName { get; set; }

        public object Value { get; set; }
    }

    public sealed class VariableSetNodeFactory : BaseNodeFactory<VariableSetNodeConfig>
    {
        public const string TypeName = "variable.set";

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
                VariableName = GetStringSetting(definition, "VariableName", null),
                Value = GetSetting(definition, "Value", null)
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

            var variableName = ResolveString(context, "VariableName", _config.VariableName);
            if (string.IsNullOrWhiteSpace(variableName))
            {
                return Task.FromResult(NodeExecutionResult.Failure("VariableName is required."));
            }

            var value = ResolveValue(context);

            context.Variables.Set(variableName, value);
            return Task.FromResult(
                NodeExecutionResult.Success(
                    "Next",
                    new Dictionary<string, object>
                    {
                        { "VariableName", variableName },
                        { "Value", value }
                    }));
        }

        private static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = context.GetInputValue(name);
            return value == null ? defaultValue : Convert.ToString(value);
        }

        private object ResolveValue(FlowExecutionContext context)
        {
            if (context.Node.InputBindings != null && context.Node.InputBindings.ContainsKey("Value"))
            {
                return context.GetInputValue("Value");
            }

            if (context.Node.Settings != null && context.Node.Settings.ContainsKey("Value"))
            {
                return context.GetInputValue("Value");
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
                        Description = "Continues after writing the variable."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Error",
                        DisplayName = "Error",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes missing variable names."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "VariableName",
                        DisplayName = "Variable Name",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Name of the variable to write."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "Value",
                        DisplayName = "Value",
                        DataType = "Object",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Value to store. This can also be provided by an input binding."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "VariableName",
                        DisplayName = "Variable Name",
                        DataType = "String",
                        Description = "The variable name that was written."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Value",
                        DisplayName = "Value",
                        DataType = "Object",
                        Description = "The value that was written."
                    }
                }
            };
        }
    }
}
