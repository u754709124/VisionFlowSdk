using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    // 共享辅助方法保证设置、绑定和标量校验行为一致。
    public sealed partial class FlowValidator
    {
        private static bool HasConfiguredValue(NodeDefinition node, string settingName)
        {
            NodeSettingValue value;
            return TryGetIgnoreCase(node.Settings, settingName, out value) && !IsValueMissing(value);
        }

        private static bool IsValueMissing(object value)
        {
            if (value == null)
            {
                return true;
            }

            var text = value as string;
            if (text != null)
            {
                return string.IsNullOrWhiteSpace(text);
            }

            var setting = value as NodeSettingValue;
            if (setting != null)
            {
                if (setting.Mode == NodeSettingValueMode.Variable)
                {
                    return setting.Selector == null || setting.Selector.Path == null || setting.Selector.Path.Count == 0;
                }

                return IsValueMissing(setting.ConstantValue);
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                return dictionary.Count == 0;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (var item in enumerable)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static bool ContainsPort(IList<NodePortDescriptor> ports, string portName)
        {
            return ports != null && ports.Any(x => x != null && string.Equals(x.Name, portName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsOutput(IList<NodeOutputDescriptor> outputs, string outputName)
        {
            return outputs != null && outputs.Any(x => x != null && string.Equals(x.Name, outputName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetIgnoreCase<TValue>(
            IDictionary<string, TValue> dictionary,
            string key,
            out TValue value)
        {
            value = default(TValue);
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (var item in dictionary)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool HasSetting(NodeDefinition node, string name)
        {
            NodeSettingValue value;
            return node != null && TryGetIgnoreCase(node.Settings, name, out value);
        }

        private static string GetSettingString(NodeDefinition node, string name, string defaultValue)
        {
            object value;
            if (!TryGetConstantSettingValue(node, name, out value) || value == null)
            {
                return defaultValue;
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void ValidatePositiveInt(
            NodeDefinition node,
            string name,
            int defaultValue,
            string fieldPrefix,
            FlowValidationResult result)
        {
            int value;
            if (!TryGetSettingInt32(node, name, defaultValue, out value, result, fieldPrefix + name))
            {
                return;
            }

            if (value <= 0)
            {
                result.AddError(
                    FlowValidationIssueCodes.SettingValueInvalid,
                    name + " must be greater than zero.",
                    nodeId: node.Id,
                    field: fieldPrefix + name);
            }
        }

        private static void ValidateNonNegativeInt(
            NodeDefinition node,
            string name,
            int defaultValue,
            string fieldPrefix,
            FlowValidationResult result)
        {
            int value;
            if (!TryGetSettingInt32(node, name, defaultValue, out value, result, fieldPrefix + name))
            {
                return;
            }

            if (value < 0)
            {
                result.AddError(
                    FlowValidationIssueCodes.SettingValueInvalid,
                    name + " must be greater than or equal to zero.",
                    nodeId: node.Id,
                    field: fieldPrefix + name);
            }
        }

        private static bool TryGetSettingInt32(
            NodeDefinition node,
            string name,
            int defaultValue,
            out int value,
            FlowValidationResult result,
            string field)
        {
            object rawValue;
            if (!TryGetConstantSettingValue(node, name, out rawValue) || rawValue == null)
            {
                value = defaultValue;
                return true;
            }

            try
            {
                value = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                value = defaultValue;
                result.AddError(FlowValidationIssueCodes.SettingValueInvalid, name + " must be an Int32 value.", nodeId: node.Id, field: field);
                return false;
            }
        }

        private static bool TryGetSettingBoolean(
            NodeDefinition node,
            string name,
            out bool value,
            FlowValidationResult result,
            string field)
        {
            object rawValue;
            if (!TryGetConstantSettingValue(node, name, out rawValue) || rawValue == null)
            {
                value = false;
                return true;
            }

            try
            {
                value = Convert.ToBoolean(rawValue, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception)
            {
                value = false;
                result.AddError(FlowValidationIssueCodes.SettingValueInvalid, name + " must be a Boolean value.", nodeId: node.Id, field: field);
                return false;
            }
        }

        private static bool IsOneOf(string value, params string[] allowedValues)
        {
            for (var index = 0; index < allowedValues.Length; index++)
            {
                if (string.Equals(value, allowedValues[index], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetConstantSettingValue(NodeDefinition node, string name, out object value)
        {
            value = null;
            NodeSettingValue setting;
            if (node == null || !TryGetIgnoreCase(node.Settings, name, out setting) || setting == null ||
                setting.Mode != NodeSettingValueMode.Constant)
            {
                return false;
            }

            value = setting.ConstantValue;
            return true;
        }

        private static bool IsSimpleValue(object value)
        {
            if (value == null)
            {
                return true;
            }

            var type = value.GetType();
            return type.IsPrimitive ||
                type.IsEnum ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) ||
                type == typeof(Guid);
        }
    }
}

