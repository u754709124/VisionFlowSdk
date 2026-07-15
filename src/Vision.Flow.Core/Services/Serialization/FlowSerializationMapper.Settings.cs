using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        private static Dictionary<string, NodeSettingValue> ToSettingDictionary(object value)
        {
            var result = new Dictionary<string, NodeSettingValue>(StringComparer.OrdinalIgnoreCase);
            var dictionary = AsDictionaryOrNull(value);
            if (dictionary == null)
            {
                return result;
            }

            foreach (var item in dictionary)
            {
                result[item.Key] = ToSettingValue(item.Value);
            }

            return result;
        }

        private static NodeSettingValue ToSettingValue(object value)
        {
            var dictionary = AsDictionary(value);
            NodeSettingValueMode mode;
            var modeText = GetString(dictionary, "Mode");
            if (!Enum.TryParse(modeText, true, out mode) || !Enum.IsDefined(typeof(NodeSettingValueMode), mode))
            {
                throw new InvalidOperationException("Node setting Mode must be Constant or Variable.");
            }

            var setting = new NodeSettingValue
            {
                Mode = mode,
                ConstantValue = GetObject(dictionary, "ConstantValue")
            };

            object selectorValue;
            if (TryGetValue(dictionary, "Selector", out selectorValue) && selectorValue != null)
            {
                setting.Selector = ToVariableSelector(selectorValue);
            }

            return setting;
        }

        private static VariableSelector ToVariableSelector(object value)
        {
            var dictionary = AsDictionary(value);
            VariableSelectorScope scope;
            var scopeText = GetString(dictionary, "Scope");
            if (!Enum.TryParse(scopeText, true, out scope) || !Enum.IsDefined(typeof(VariableSelectorScope), scope))
            {
                throw new InvalidOperationException("Variable selector Scope is invalid.");
            }

            var selector = new VariableSelector { Scope = scope };
            object pathValue;
            if (TryGetValue(dictionary, "Path", out pathValue))
            {
                foreach (var segment in AsEnumerable(pathValue))
                {
                    selector.Path.Add(Convert.ToString(segment));
                }
            }

            return selector;
        }

        private static object ToSerializableSettings(IDictionary<string, NodeSettingValue> settings)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (settings == null)
            {
                return result;
            }

            foreach (var item in settings)
            {
                result[item.Key] = ToSerializableSettingValue(item.Value);
            }

            return result;
        }

        private static object ToSerializableSettingValue(NodeSettingValue setting)
        {
            if (setting == null)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                { "Mode", setting.Mode.ToString() },
                { "ConstantValue", NormalizeObject(setting.ConstantValue) },
                { "Selector", ToSerializableSelector(setting.Selector) }
            };
        }

        private static object ToSerializableSelector(VariableSelector selector)
        {
            if (selector == null)
            {
                return null;
            }

            return new Dictionary<string, object>
            {
                { "Scope", selector.Scope.ToString() },
                { "Path", selector.Path == null ? new List<string>() : new List<string>(selector.Path) }
            };
        }
    }
}
