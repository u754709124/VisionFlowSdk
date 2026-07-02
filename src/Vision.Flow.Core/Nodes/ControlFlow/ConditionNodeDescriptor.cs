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
                DisplayName = "Condition",
                Category = "Flow",
                Version = "1.0.0",
                Description = "Routes execution through True or False according to a configured comparison.",
                InputPorts =
                {
                    CreatePort("In", "In", "Input", "Execution input.")
                },
                OutputPorts =
                {
                    CreatePort("True", "True", "Output", "Condition matched."),
                    CreatePort("False", "False", "Output", "Condition did not match."),
                    CreatePort("Error", "Error", "Output", "Condition evaluation failed.")
                },
                Settings =
                {
                    CreateStringSetting("LeftBinding", "Left Binding", null, true, "Expression used to resolve the left value."),
                    CreateStringSetting("Operator", "Operator", "Equal", true, "Equal, NotEqual, GreaterThan, LessThan, Contains, IsNull, or IsNotNull."),
                    CreateObjectSetting("RightValue", "Right Value", null, false, "Constant right value."),
                    CreateStringSetting("RightBinding", "Right Binding", null, false, "Optional expression used to resolve the right value.")
                },
                Outputs =
                {
                    CreateOutput("Result", "Result", "Boolean", "Condition evaluation result."),
                    CreateOutput("IsMatched", "Is Matched", "Boolean", "Alias for Result."),
                    CreateOutput("Left", "Left", "Object", "Resolved left value."),
                    CreateOutput("Right", "Right", "Object", "Resolved right value."),
                    CreateOutput("Operator", "Operator", "String", "Operator used for evaluation.")
                }
            };
        }

        private static NodePortDescriptor CreatePort(string name, string displayName, string direction, string description)
        {
            return new NodePortDescriptor
            {
                Name = name,
                DisplayName = displayName,
                Direction = direction,
                DataType = "Control",
                IsRequired = string.Equals(direction, "Input", StringComparison.OrdinalIgnoreCase),
                Description = description
            };
        }

        private static NodeSettingDescriptor CreateStringSetting(string name, string displayName, string defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "String",
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
                DataType = "Object",
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        private static NodeOutputDescriptor CreateOutput(string name, string displayName, string dataType, string description)
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
