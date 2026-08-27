using System;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public static class ConditionNodeDescriptor
    {
        /// <summary>创建 3.0 强类型条件判断节点描述符。</summary>
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = ConditionNodeFactory.TypeName,
                DisplayName = "条件判断",
                Category = "流程控制",
                Version = "3.0.0",
                Description = "根据配置的比较条件从真或假分支继续执行。",
                InputPorts =
                {
                    CreatePort(FlowPortNames.In, "输入", FlowPortDirection.Input, "接收控制流输入。")
                },
                OutputPorts =
                {
                    CreatePort(FlowPortNames.True, "真", FlowPortDirection.Output, "条件成立时继续执行。"),
                    CreatePort(FlowPortNames.False, "假", FlowPortDirection.Output, "条件不成立时继续执行。"),
                    CreatePort(FlowPortNames.Error, "错误", FlowPortDirection.Output, "操作数或操作符无效时继续执行。")
                },
                Settings =
                {
                    CreateOperandSetting(FlowSettingNames.LeftValue, "左值", NodeSettingBindingMode.VariableOnly, "必须绑定数值、字符串或枚举变量。"),
                    CreateOperatorSetting(),
                    CreateOperandSetting(FlowSettingNames.RightValue, "右值", NodeSettingBindingMode.ConstantOrVariable, "输入固定值或绑定与左值兼容的变量。")
                },
                Outputs =
                {
                    CreateOutput(FlowOutputNames.IsMatched, "是否匹配", FlowDataType.Boolean, "条件判断结果。"),
                    CreateOutput("Left", "左值", FlowDataType.Object, "本次执行解析后的左操作数，仅用于诊断。"),
                    CreateOutput("Right", "右值", FlowDataType.Object, "本次执行解析后的右操作数，仅用于诊断。"),
                    CreateOutput(FlowSettingNames.Operator, "操作符", FlowDataType.String, "本次执行使用的操作符，仅用于诊断。", typeof(ConditionOperator))
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

        private static NodeSettingDescriptor CreateOperatorSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = FlowSettingNames.Operator,
                DisplayName = "操作符",
                DataType = FlowDataType.String,
                EnumType = typeof(ConditionOperator),
                DefaultValue = FlowEnumConverter.ToWireValue(ConditionOperator.Equal),
                IsRequired = true,
                Description = "数值支持六种关系操作符；字符串和枚举仅支持等于与不等于。",
                BindingMode = NodeSettingBindingMode.ConstantOnly,
                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                AllowedVariableSources = VariableSelectorScopeFlags.None
            };
        }

        private static NodeSettingDescriptor CreateOperandSetting(
            string name,
            string displayName,
            NodeSettingBindingMode bindingMode,
            string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = FlowDataType.Object,
                DefaultValue = null,
                IsRequired = true,
                Description = description,
                BindingMode = bindingMode,
                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                AllowedVariableSources = VariableSelectorScopeFlags.All,
                VariableTypeValidator = ValidateComparableVariableType
            };
        }

        private static string ValidateComparableVariableType(
            FlowDataType dataType,
            Type enumType,
            Type objectType)
        {
            if (dataType == FlowDataType.Int32 ||
                dataType == FlowDataType.Int64 ||
                dataType == FlowDataType.Double)
            {
                return null;
            }

            if (dataType == FlowDataType.String && objectType == null)
                return null;

            return "条件操作数只支持数值、字符串或枚举类型。";
        }

        private static NodeOutputDescriptor CreateOutput(string name, string displayName, FlowDataType dataType, string description, Type enumType = null)
        {
            return new NodeOutputDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = dataType,
                EnumType = enumType,
                Description = description
            };
        }
    }
}
