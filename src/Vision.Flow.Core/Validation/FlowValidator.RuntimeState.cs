using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Vision.Flow.Core
{
    // Runtime-state checks prevent designer-only view state from leaking into .flowruntime files.
    public sealed partial class FlowValidator
    {
        private static void ValidateNoDesignerState(RuntimeFlowDefinition definition, FlowValidationResult result)
        {
            CheckNoDesignerStateValue("Settings", definition.Settings, result, 0);

            var nodes = definition.Nodes ?? new List<NodeDefinition>();
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null)
                {
                    continue;
                }

                CheckNoDesignerStateValue("Nodes[" + index + "].Settings", node.Settings, result, 0);
                if (node.InputBindings == null)
                {
                    continue;
                }

                foreach (var binding in node.InputBindings)
                {
                    if (binding.Value != null && binding.Value.IsConstant)
                    {
                        CheckNoDesignerStateValue("Nodes[" + index + "].InputBindings." + binding.Key + ".ConstantValue", binding.Value.ConstantValue, result, 0);
                    }
                }
            }
        }

        private static void CheckNoDesignerStateValue(
            string field,
            object value,
            FlowValidationResult result,
            int depth)
        {
            if (value == null || depth > 8 || IsSimpleValue(value))
            {
                return;
            }

            if (value is FlowDesignDocument || value is FlowViewState || value is NodeViewState)
            {
                result.AddError(
                    "RuntimeContainsViewState",
                    "Runtime flow must not contain designer view state.",
                    field: field);
                return;
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry item in dictionary)
                {
                    var key = Convert.ToString(item.Key, CultureInfo.InvariantCulture);
                    CheckNoDesignerStateValue(field + "." + key, item.Value, result, depth + 1);
                }

                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    CheckNoDesignerStateValue(field + "[" + index + "]", item, result, depth + 1);
                    index++;
                }
            }
        }
    }
}
