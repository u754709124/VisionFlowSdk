using System;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public static class ConditionNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = ConditionNodeFactory.TypeName,
                DisplayName = "条件判断",
                Category = "流程控制",
                Version = "1.0.0",
                Description = "根据配置的比较条件从真或假分支继续执行。",
                InputPorts =
                {
                    CreatePort(FlowPortNames.In, FlowPortNames.In, FlowPortDirection.Input, "Execution input.")
                },
                OutputPorts =
                {
                    CreatePort(FlowPortNames.True, FlowPortNames.True, FlowPortDirection.Output, "Condition matched."),
                    CreatePort(FlowPortNames.False, FlowPortNames.False, FlowPortDirection.Output, "Condition did not match."),
                    CreatePort(FlowPortNames.Error, FlowPortNames.Error, FlowPortDirection.Output, "Condition evaluation failed.")
                },
                Settings =
                {
                    CreateStringSetting(FlowSettingNames.LeftBinding, "Left Binding", null, true, "Expression used to resolve the left value."),
                    CreateStringSetting(FlowSettingNames.Operator, "Operator", FlowEnumConverter.ToWireValue(ConditionOperator.Equal), true, "Equal, NotEqual, GreaterThan, LessThan, Contains, IsNull, or IsNotNull."),
                    CreateObjectSetting(FlowSettingNames.RightValue, "Right Value", null, false, "Constant right value."),
                    CreateStringSetting(FlowSettingNames.RightBinding, "Right Binding", null, false, "Optional expression used to resolve the right value.")
                },
                Outputs =
                {
                    CreateOutput(FlowOutputNames.Result, "Result", FlowDataType.Boolean, "Condition evaluation result."),
                    CreateOutput(FlowOutputNames.IsMatched, "Is Matched", FlowDataType.Boolean, "Alias for Result."),
                    CreateOutput("Left", "Left", FlowDataType.Object, "Resolved left value."),
                    CreateOutput("Right", "Right", FlowDataType.Object, "Resolved right value."),
                    CreateOutput(FlowSettingNames.Operator, "Operator", FlowDataType.String, "Operator used for evaluation.")
                }
            };
        }

        private static NodePortDescriptor CreatePort(string name, string displayName, FlowPortDirection direction, string description)
        {
            return new NodePortDescriptor
            {
                Name = name,
                DisplayName = displayName,
                Direction = direction,
                DataType = FlowDataType.Control,
                IsRequired = direction == FlowPortDirection.Input,
                Description = description
            };
        }

        private static NodeSettingDescriptor CreateStringSetting(string name, string displayName, string defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = FlowDataType.String,
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        private static NodeSettingDescriptor CreateObjectSetting(string name, string displayName, object defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = FlowDataType.Object,
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        private static NodeOutputDescriptor CreateOutput(string name, string displayName, FlowDataType dataType, string description)
        {
            return new NodeOutputDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = dataType,
                Description = description
            };
        }
    }
}
