using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Validation
{
    // 运行态检查防止设计器专用视图状态泄漏到 .flowruntime 文件。
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

                if (node.Settings == null)
                {
                    continue;
                }

                foreach (var setting in node.Settings)
                {
                    if (setting.Value != null)
                    {
                        CheckNoDesignerStateValue("Nodes[" + index + "].Settings." + setting.Key + ".ConstantValue", setting.Value.ConstantValue, result, 0);
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
                    FlowValidationIssueCodes.RuntimeContainsViewState,
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

