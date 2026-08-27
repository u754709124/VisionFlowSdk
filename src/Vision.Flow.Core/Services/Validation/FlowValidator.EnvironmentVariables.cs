using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
        private static IDictionary<string, EnvironmentVariableDefinition>
            ValidateEnvironmentVariables(
                IList<EnvironmentVariableDefinition> definitions,
                FlowValidationResult result)
        {
            var byId = new Dictionary<string, EnvironmentVariableDefinition>(
                StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var source = definitions ?? new List<EnvironmentVariableDefinition>();
            for (var index = 0; index < source.Count; index++)
            {
                var definition = source[index];
                var field = "EnvironmentVariables[" + index + "]";
                if (definition == null)
                {
                    result.AddError(
                        FlowValidationIssueCodes.EnvironmentVariableInvalid,
                        "Environment variable definition must not be null.",
                        field: field);
                    continue;
                }

                var id = (definition.Id ?? string.Empty).Trim();
                var name = (definition.Name ?? string.Empty).Trim();
                if (id.Length == 0 || byId.ContainsKey(id))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EnvironmentVariableIdInvalid,
                        "Environment variable Id must be non-empty and unique.",
                        field: field + ".Id");
                }
                else
                {
                    byId.Add(id, definition);
                }
                if (name.Length == 0 || !names.Add(name))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EnvironmentVariableNameInvalid,
                        "Environment variable Name must be non-empty and unique.",
                        field: field + ".Name");
                }

                if (definition.DataType != FlowDataType.Int32 &&
                    definition.DataType != FlowDataType.Boolean &&
                    definition.DataType != FlowDataType.String &&
                    definition.DataType != FlowDataType.DateTime)
                {
                    result.AddError(
                        FlowValidationIssueCodes.EnvironmentVariableTypeInvalid,
                        "Environment variable type must be Int32, Boolean, String or DateTime.",
                        field: field + ".DataType");
                    continue;
                }

                try
                {
                    EnvironmentVariableValues.ConvertValue(
                        definition.DefaultValue,
                        definition.DataType);
                }
                catch (ArgumentException)
                {
                    result.AddError(
                        FlowValidationIssueCodes.EnvironmentVariableDefaultInvalid,
                        "Environment variable DefaultValue is required and must match DataType.",
                        field: field + ".DefaultValue");
                }
            }
            return byId;
        }
    }
}
