using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
        private static IDictionary<string, GlobalVariableDefinition>
            ValidateGlobalVariables(
                IList<GlobalVariableDefinition> definitions,
                FlowValidationResult result)
        {
            var byId = new Dictionary<string, GlobalVariableDefinition>(
                StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var source = definitions ?? new List<GlobalVariableDefinition>();
            for (var index = 0; index < source.Count; index++)
            {
                GlobalVariableDefinition definition = source[index];
                string field = "GlobalVariables[" + index + "]";
                if (definition == null)
                {
                    result.AddError(
                        FlowValidationIssueCodes.GlobalVariableInvalid,
                        "Global variable definition must not be null.",
                        field: field);
                    continue;
                }

                string id = (definition.Id ?? string.Empty).Trim();
                string name = (definition.Name ?? string.Empty).Trim();
                if (id.Length == 0 || byId.ContainsKey(id))
                {
                    result.AddError(
                        FlowValidationIssueCodes.GlobalVariableIdInvalid,
                        "Global variable Id must be non-empty and unique.",
                        field: field + ".Id");
                }
                else
                {
                    byId.Add(id, definition);
                }

                if (name.Length == 0 || !names.Add(name))
                {
                    result.AddError(
                        FlowValidationIssueCodes.GlobalVariableNameInvalid,
                        "Global variable Name must be non-empty and unique.",
                        field: field + ".Name");
                }

                if (definition.DataType != FlowDataType.Int32 &&
                    definition.DataType != FlowDataType.Boolean &&
                    definition.DataType != FlowDataType.String &&
                    definition.DataType != FlowDataType.DateTime)
                {
                    result.AddError(
                        FlowValidationIssueCodes.GlobalVariableTypeInvalid,
                        "Global variable type must be Int32, Boolean, String or DateTime.",
                        field: field + ".DataType");
                    continue;
                }

                try
                {
                    GlobalVariableValues.ConvertValue(
                        definition.DefaultValue,
                        definition.DataType);
                }
                catch (ArgumentException)
                {
                    result.AddError(
                        FlowValidationIssueCodes.GlobalVariableDefaultInvalid,
                        "Global variable DefaultValue is required and must match DataType.",
                        field: field + ".DefaultValue");
                }
            }
            return byId;
        }
    }
}
