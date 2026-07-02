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
                DisplayName = "AND Join",
                Category = "Flow",
                Version = "1.0.0",
                Description = "Collects multiple inputs by JoinKey and continues when all expected inputs arrive.",
                InputPorts =
                {
                    CreatePort("In", "In", "Input", "Execution input.")
                },
                OutputPorts =
                {
                    CreatePort("Next", "Next", "Output", "All expected inputs have arrived."),
                    CreatePort("Error", "Error", "Output", "Join configuration or duplicate input error.")
                },
                Settings =
                {
                    CreateStringSetting("JoinKeyBinding", "Join Key Binding", null, true, "Expression used to resolve the join key, for example {{ token.PositionId }}."),
                    CreateIntSetting("ExpectedInputCount", "Expected Inputs", 2, true, "Number of inputs required for the join key."),
                    CreateIntSetting("TimeoutMs", "Timeout (ms)", 0, false, "Reserved timeout. Zero disables timeout handling."),
                    CreateStringSetting("DuplicatePolicy", "Duplicate Policy", "Ignore", true, "Ignore, Replace, or Error when the same token arrives twice.")
                },
                Outputs =
                {
                    CreateOutput("Result", "Result", "Boolean", "True when the join completes."),
                    CreateOutput("IsMatched", "Is Matched", "Boolean", "True when all inputs are matched."),
                    CreateOutput("JoinKey", "Join Key", "String", "Resolved join key."),
                    CreateOutput("ActualInputCount", "Actual Inputs", "Int32", "Number of inputs currently collected."),
                    CreateOutput("ExpectedInputCount", "Expected Inputs", "Int32", "Expected input count.")
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

        private static NodeSettingDescriptor CreateIntSetting(string name, string displayName, int defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "Int32",
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
