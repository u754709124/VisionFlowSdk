using System;
using System.Collections;
using System.Collections.Generic;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 将一个结构化变量来源映射到稳定的目标字段名；映射顺序与流程文件中的顺序一致。
    /// </summary>
    public sealed class VariableSelectorFieldMapping
    {
        /// <summary>获取或设置不会随来源重命名而变化的目标字段名。</summary>
        public string AttributeName { get; set; }

        /// <summary>获取或设置映射值的结构化变量来源。</summary>
        public VariableSelector Source { get; set; }

        /// <summary>
        /// 从流程设置常量读取有序映射集合，同时兼容反序列化后的字典对象。
        /// </summary>
        public static IList<VariableSelectorFieldMapping> ReadCollection(object value)
        {
            var result = new List<VariableSelectorFieldMapping>();
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                return result;
            }

            foreach (var item in enumerable)
            {
                result.Add(Read(item));
            }

            return result;
        }

        /// <summary>将映射转换为只包含稳定协议字段的可序列化字典。</summary>
        public IDictionary<string, object> ToSerializableObject()
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "AttributeName", AttributeName },
                { "Source", Source == null ? null : new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "Scope", Source.Scope.ToString() },
                        { "Path", Source.Path == null ? new List<string>() : new List<string>(Source.Path) }
                    }
                }
            };
        }

        private static VariableSelectorFieldMapping Read(object value)
        {
            var typed = value as VariableSelectorFieldMapping;
            if (typed != null)
            {
                return new VariableSelectorFieldMapping
                {
                    AttributeName = typed.AttributeName,
                    Source = CloneSelector(typed.Source)
                };
            }

            var dictionary = value as IDictionary<string, object>;
            if (dictionary == null)
            {
                throw new InvalidOperationException("Field mapping must be an object.");
            }

            object attributeName;
            object source;
            TryGetValue(dictionary, "AttributeName", out attributeName);
            TryGetValue(dictionary, "Source", out source);
            return new VariableSelectorFieldMapping
            {
                AttributeName = attributeName == null ? null : Convert.ToString(attributeName),
                Source = ReadSelector(source)
            };
        }

        private static VariableSelector ReadSelector(object value)
        {
            var typed = value as VariableSelector;
            if (typed != null)
            {
                return CloneSelector(typed);
            }

            var dictionary = value as IDictionary<string, object>;
            if (dictionary == null)
            {
                return null;
            }

            object scopeValue;
            VariableSelectorScope scope;
            if (!TryGetValue(dictionary, "Scope", out scopeValue) ||
                !Enum.TryParse(Convert.ToString(scopeValue), true, out scope) ||
                !Enum.IsDefined(typeof(VariableSelectorScope), scope))
            {
                throw new InvalidOperationException("Field mapping Source.Scope is invalid.");
            }

            var selector = new VariableSelector { Scope = scope };
            object pathValue;
            if (TryGetValue(dictionary, "Path", out pathValue))
            {
                var path = pathValue as IEnumerable;
                if (path != null && !(pathValue is string))
                {
                    foreach (var segment in path)
                    {
                        selector.Path.Add(segment == null ? null : Convert.ToString(segment));
                    }
                }
            }

            return selector;
        }

        private static VariableSelector CloneSelector(VariableSelector selector)
        {
            return selector == null
                ? null
                : new VariableSelector
                {
                    Scope = selector.Scope,
                    Path = selector.Path == null ? new List<string>() : new List<string>(selector.Path)
                };
        }

        private static bool TryGetValue(IDictionary<string, object> dictionary, string key, out object value)
        {
            foreach (var item in dictionary)
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
