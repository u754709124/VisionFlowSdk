using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 将结构化变量选择器解析为当前节点执行可使用的值。
    /// </summary>
    public interface ISettingValueResolver
    {
        object Resolve(VariableSelector selector, FlowExecutionContext context);
    }

    /// <summary>
    /// 默认解析器，支持上游节点输出和当前 Token，TriggerInput 留待统一触发运行时实现。
    /// </summary>
    public sealed class DefaultSettingValueResolver : ISettingValueResolver
    {
        public static readonly DefaultSettingValueResolver Instance = new DefaultSettingValueResolver();

        private DefaultSettingValueResolver()
        {
        }

        public object Resolve(VariableSelector selector, FlowExecutionContext context)
        {
            if (selector == null)
            {
                throw new ArgumentNullException("selector");
            }

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var path = selector.Path ?? new List<string>();
            switch (selector.Scope)
            {
                case VariableSelectorScope.NodeOutput:
                    return ResolveNodeOutput(path, context);
                case VariableSelectorScope.Token:
                    return ResolveToken(path, context);
                case VariableSelectorScope.TriggerInput:
                    throw new NotSupportedException("TriggerInput selectors are reserved for the unified trigger runtime.");
                default:
                    throw new InvalidOperationException("Unsupported variable selector scope: " + selector.Scope);
            }
        }

        private static object ResolveNodeOutput(IList<string> path, FlowExecutionContext context)
        {
            if (path.Count < 2 || string.IsNullOrWhiteSpace(path[0]) || string.IsNullOrWhiteSpace(path[1]))
            {
                throw new InvalidOperationException("NodeOutput selector Path must contain node id and output name.");
            }

            var value = context.Variables.Get(path[0] + "." + path[1]);
            return ResolveNestedPath(value, path, 2);
        }

        private static object ResolveToken(IList<string> path, FlowExecutionContext context)
        {
            if (path.Count == 0 || string.IsNullOrWhiteSpace(path[0]))
            {
                throw new InvalidOperationException("Token selector Path must not be empty.");
            }

            object value;
            var first = path[0];
            if (string.Equals(first, "Values", StringComparison.OrdinalIgnoreCase))
            {
                value = context.Token.Values;
            }
            else if (string.Equals(first, "Metadata", StringComparison.OrdinalIgnoreCase))
            {
                value = context.Token.Metadata;
            }
            else
            {
                var property = typeof(Runtime.State.FlowToken).GetProperty(first, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property != null && property.CanRead)
                {
                    value = property.GetValue(context.Token, null);
                }
                else
                {
                    value = context.Token.Get(first);
                }
            }

            return ResolveNestedPath(value, path, 1);
        }

        private static object ResolveNestedPath(object value, IList<string> path, int startIndex)
        {
            for (var index = startIndex; index < path.Count; index++)
            {
                var segment = path[index];
                if (value == null)
                {
                    throw new InvalidOperationException("Variable selector cannot traverse null at '" + segment + "'.");
                }

                var dictionary = value as IDictionary;
                if (dictionary != null)
                {
                    object dictionaryValue;
                    if (!TryGetDictionaryValue(dictionary, segment, out dictionaryValue))
                    {
                        throw new KeyNotFoundException("Variable selector path was not found: " + segment);
                    }

                    value = dictionaryValue;
                    continue;
                }

                var list = value as IList;
                int listIndex;
                if (list != null && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out listIndex))
                {
                    if (listIndex < 0 || listIndex >= list.Count)
                    {
                        throw new IndexOutOfRangeException("Variable selector index is out of range: " + segment);
                    }

                    value = list[listIndex];
                    continue;
                }

                var property = value.GetType().GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (property == null || !property.CanRead)
                {
                    throw new KeyNotFoundException("Variable selector path was not found: " + segment);
                }

                value = property.GetValue(value, null);
            }

            return value;
        }

        private static bool TryGetDictionaryValue(IDictionary dictionary, string key, out object value)
        {
            value = null;
            foreach (DictionaryEntry item in dictionary)
            {
                if (string.Equals(Convert.ToString(item.Key, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
