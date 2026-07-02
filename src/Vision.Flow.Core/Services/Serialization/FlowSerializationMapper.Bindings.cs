using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        private static Dictionary<string, VariableBinding> ToBindingDictionary(object value)
        {
            var result = new Dictionary<string, VariableBinding>(StringComparer.Ordinal);
            foreach (var item in ToObjectDictionary(value))
            {
                var binding = item.Value as VariableBinding;
                if (binding != null)
                {
                    result[item.Key] = binding;
                    continue;
                }

                var text = item.Value as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result[item.Key] = VariableBinding.ForExpression(text);
                    continue;
                }

                var dictionary = item.Value as IDictionary<string, object>;
                if (dictionary != null)
                {
                    result[item.Key] = ToVariableBinding(dictionary);
                }
            }

            return result;
        }

        private static VariableBinding ToVariableBinding(IDictionary<string, object> dictionary)
        {
            var binding = new VariableBinding
            {
                Expression = GetString(dictionary, "Expression"),
                SourceNodeId = GetString(dictionary, "SourceNodeId"),
                SourceOutputName = GetString(dictionary, "SourceOutputName"),
                ConstantValue = GetObject(dictionary, "ConstantValue"),
                ValueType = GetString(dictionary, "ValueType"),
                IsConstant = GetBoolean(dictionary, "IsConstant", false)
            };

            if (!binding.IsConstant &&
                string.IsNullOrWhiteSpace(binding.SourceNodeId) &&
                string.IsNullOrWhiteSpace(binding.SourceOutputName) &&
                !string.IsNullOrWhiteSpace(binding.Expression))
            {
                return VariableBinding.ForExpression(binding.Expression);
            }

            return binding;
        }
    }
}
