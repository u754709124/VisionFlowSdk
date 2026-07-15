using System;
using System.Threading;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public static class AndJoinNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = AndJoinNodeFactory.TypeName,
                DisplayName = "与汇合",
                Category = "流程控制",
                Version = "1.0.0",
                Description = "按汇合键收集多个输入，全部到达后继续执行。",
                InputPorts =
                {
                    CreatePort(FlowPortNames.In, FlowPortNames.In, FlowPortDirection.Input, "Execution input.")
                },
                OutputPorts =
                {
                    CreatePort(FlowPortNames.Next, FlowPortNames.Next, FlowPortDirection.Output, "All expected inputs have arrived."),
                    CreatePort(FlowPortNames.Error, FlowPortNames.Error, FlowPortDirection.Output, "Join configuration or duplicate input error.")
                },
                Settings =
                {
                    CreateStringSetting(FlowSettingNames.JoinKeyBinding, "Join Key", null, true, "Join key value or variable selector."),
                    CreateIntSetting(FlowSettingNames.ExpectedInputCount, "Expected Inputs", 2, true, "Number of inputs required for the join key."),
                    CreateIntSetting(FlowSettingNames.TimeoutMs, "Timeout (ms)", 0, false, "Reserved timeout. Zero disables timeout handling."),
                    CreateStringSetting(FlowSettingNames.DuplicatePolicy, "Duplicate Policy", FlowEnumConverter.ToWireValue(FlowDuplicatePolicy.Ignore), true, "Ignore, Replace, or Error when the same token arrives twice.")
                },
                Outputs =
                {
                    CreateOutput(FlowOutputNames.Result, "Result", FlowDataType.Boolean, "True when the join completes."),
                    CreateOutput(FlowOutputNames.IsMatched, "Is Matched", FlowDataType.Boolean, "True when all inputs are matched."),
                    CreateOutput(FlowOutputNames.JoinKey, "Join Key", FlowDataType.String, "Resolved join key."),
                    CreateOutput(FlowOutputNames.ActualInputCount, "Actual Inputs", FlowDataType.Int32, "Number of inputs currently collected."),
                    CreateOutput(FlowOutputNames.ExpectedInputCount, "Expected Inputs", FlowDataType.Int32, "Expected input count.")
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
                Description = description,
                BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                AllowedVariableSources = VariableSelectorScopeFlags.NodeOutput | VariableSelectorScopeFlags.Token
            };
        }

        private static NodeSettingDescriptor CreateIntSetting(string name, string displayName, int defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = FlowDataType.Int32,
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description,
                BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                AllowedVariableSources = VariableSelectorScopeFlags.NodeOutput | VariableSelectorScopeFlags.Token
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
