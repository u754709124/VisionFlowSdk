using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 将流程环境变量默认值与上位机覆盖值合并为类型安全的只读运行快照。
    /// </summary>
    public static class EnvironmentVariableValues
    {
        /// <summary>使用定义默认值和可选强类型覆盖值创建只读运行快照，不执行跨类型转换。</summary>
        public static IDictionary<string, object> CreateSnapshot(
            IEnumerable<EnvironmentVariableDefinition> definitions,
            IDictionary<string, object> overrides = null)
        {
            var definitionList = (definitions ??
                Enumerable.Empty<EnvironmentVariableDefinition>()).ToList();
            var definitionIds = new HashSet<string>(
                definitionList
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                    .Select(x => x.Id),
                StringComparer.OrdinalIgnoreCase);
            if (overrides != null)
            {
                foreach (var item in overrides)
                {
                    if (!definitionIds.Contains(item.Key))
                    {
                        throw new ArgumentException(
                            "Environment variable override does not match a definition: " +
                            item.Key,
                            "overrides");
                    }
                }
            }

            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitionList)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    throw new ArgumentException(
                        "Environment variable definitions must contain a non-empty Id.",
                        "definitions");
                }
                if (values.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        "Environment variable Id must be unique: " + definition.Id,
                        "definitions");
                }

                object source;
                if (overrides == null ||
                    !TryGetValue(overrides, definition.Id, out source))
                {
                    source = definition.DefaultValue;
                }
                values.Add(definition.Id, ConvertValue(source, definition.DataType));
            }

            return new ReadOnlyDictionary<string, object>(values);
        }

        /// <summary>验证值与声明类型完全一致并返回原始强类型值。</summary>
        public static object ConvertValue(object value, FlowDataType dataType)
        {
            if (value == null)
            {
                throw new ArgumentException(
                    "Environment variable values must not be null.",
                    "value");
            }

            switch (dataType)
            {
                case FlowDataType.String:
                    if (value is string)
                        return value;
                    break;
                case FlowDataType.Int32:
                    if (value is int)
                        return value;
                    break;
                case FlowDataType.Boolean:
                    if (value is bool)
                        return value;
                    break;
                case FlowDataType.DateTime:
                    if (value is DateTime)
                        return value;
                    break;
                default:
                    throw new ArgumentException(
                        "Environment variable type is not supported: " + dataType,
                        "dataType");
            }

            throw new ArgumentException(
                "Environment variable value must match " + dataType + " exactly.",
                "value");
        }

        private static bool TryGetValue(
            IDictionary<string, object> values,
            string key,
            out object value)
        {
            foreach (var item in values)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
