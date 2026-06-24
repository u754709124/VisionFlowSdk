using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vision.Flow.Core
{
    public interface IVariablePool
    {
        void Set(string name, object value);

        object Get(string name);

        T Get<T>(string name);

        bool TryGet(string name, out object value);

        bool TryGet<T>(string name, out T value);

        IDictionary<string, object> Snapshot();
    }

    public sealed class VariablePool : IVariablePool
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, object> _values;

        public VariablePool()
        {
            _values = new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public void Set(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Variable name is required.", "name");
            }

            lock (_gate)
            {
                _values[name] = value;
            }
        }

        public object Get(string name)
        {
            object value;
            if (!TryGet(name, out value))
            {
                throw new KeyNotFoundException("Variable was not found: " + name);
            }

            return value;
        }

        public T Get<T>(string name)
        {
            return ConvertValue<T>(Get(name), name);
        }

        public bool TryGet(string name, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            lock (_gate)
            {
                return _values.TryGetValue(name, out value);
            }
        }

        public bool TryGet<T>(string name, out T value)
        {
            object rawValue;
            if (TryGet(name, out rawValue))
            {
                value = ConvertValue<T>(rawValue, name);
                return true;
            }

            value = default(T);
            return false;
        }

        public IDictionary<string, object> Snapshot()
        {
            lock (_gate)
            {
                return new Dictionary<string, object>(_values, StringComparer.Ordinal);
            }
        }

        private static T ConvertValue<T>(object value, string name)
        {
            if (value == null)
            {
                return default(T);
            }

            if (value is T)
            {
                return (T)value;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("Variable '" + name + "' cannot be converted to " + typeof(T).FullName + ".", ex);
            }
        }
    }
}
