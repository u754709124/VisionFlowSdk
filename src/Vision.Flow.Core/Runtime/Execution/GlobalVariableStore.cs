using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 使用单一临界区维护 Runner 独占的全局变量，保证多变量快照不会混合不同写入时刻。
    /// </summary>
    public sealed class GlobalVariableStore : IGlobalVariableStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, GlobalVariableDefinition> _definitions;
        private readonly Dictionary<string, object> _values;

        /// <summary>从流程定义默认值创建独立存储。</summary>
        public GlobalVariableStore(IEnumerable<GlobalVariableDefinition> definitions)
        {
            _definitions = new Dictionary<string, GlobalVariableDefinition>(
                StringComparer.OrdinalIgnoreCase);
            _values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (GlobalVariableDefinition definition in
                definitions ?? Enumerable.Empty<GlobalVariableDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    throw new ArgumentException(
                        "Global variable definitions must contain a non-empty Id.",
                        "definitions");
                }

                string id = definition.Id.Trim();
                if (_definitions.ContainsKey(id))
                {
                    throw new ArgumentException(
                        "Global variable Id must be unique: " + id,
                        "definitions");
                }

                string name = (definition.Name ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    throw new ArgumentException(
                        "Global variable definitions must contain a non-empty Name.",
                        "definitions");
                }

                if (!names.Add(name))
                {
                    throw new ArgumentException(
                        "Global variable Name must be unique: " + name,
                        "definitions");
                }

                var snapshot = new GlobalVariableDefinition
                {
                    Id = id,
                    Name = name,
                    DataType = definition.DataType,
                    DefaultValue = GlobalVariableValues.ConvertValue(
                        definition.DefaultValue,
                        definition.DataType)
                };
                _definitions.Add(id, snapshot);
                _values.Add(id, snapshot.DefaultValue);
            }
        }

        /// <summary>按稳定变量 Id 读取当前值。</summary>
        public object Get(string variableId)
        {
            string id = NormalizeId(variableId);
            lock (_gate)
            {
                object value;
                if (!_values.TryGetValue(id, out value))
                    throw new KeyNotFoundException("Global variable was not found: " + id);
                return value;
            }
        }

        /// <summary>校验声明类型后更新变量值。</summary>
        public void Set(string variableId, object value)
        {
            string id = NormalizeId(variableId);
            lock (_gate)
            {
                GlobalVariableDefinition definition;
                if (!_definitions.TryGetValue(id, out definition))
                    throw new KeyNotFoundException("Global variable was not found: " + id);
                _values[id] = GlobalVariableValues.ConvertValue(value, definition.DataType);
            }
        }

        /// <summary>在一个锁范围内复制全部指定变量。</summary>
        public IReadOnlyDictionary<string, object> CreateSnapshot(
            IEnumerable<string> variableIds)
        {
            var ids = (variableIds ?? Enumerable.Empty<string>())
                .Select(NormalizeId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            lock (_gate)
            {
                var snapshot = new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (string id in ids)
                {
                    object value;
                    if (!_values.TryGetValue(id, out value))
                        throw new KeyNotFoundException("Global variable was not found: " + id);
                    snapshot.Add(id, value);
                }
                return new ReadOnlyDictionary<string, object>(snapshot);
            }
        }

        private static string NormalizeId(string variableId)
        {
            string id = (variableId ?? string.Empty).Trim();
            if (id.Length == 0)
                throw new ArgumentException("Global variable id is required.", "variableId");
            return id;
        }
    }

    /// <summary>为不使用全局变量的直接节点测试和兼容调用提供明确的空存储。</summary>
    internal sealed class EmptyGlobalVariableStore : IGlobalVariableStore
    {
        public static readonly EmptyGlobalVariableStore Instance =
            new EmptyGlobalVariableStore();

        private EmptyGlobalVariableStore()
        {
        }

        public object Get(string variableId)
        {
            throw new KeyNotFoundException(
                "Global variable was not found: " + variableId);
        }

        public void Set(string variableId, object value)
        {
            throw new KeyNotFoundException(
                "Global variable was not found: " + variableId);
        }

        public IReadOnlyDictionary<string, object> CreateSnapshot(
            IEnumerable<string> variableIds)
        {
            return new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
