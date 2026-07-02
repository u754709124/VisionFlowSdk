using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Runtime
{
    /// <summary>
    /// 变量池接口，节点输出变量通过它写入并供后续节点按名称读取。
    /// </summary>
    public interface IVariablePool
    {
        void Set(string name, object value);

        object Get(string name);

        T Get<T>(string name);

        bool TryGet(string name, out object value);

        bool TryGet<T>(string name, out T value);

        IDictionary<string, object> Snapshot();
    }

    /// <summary>
    /// 线程安全变量池实现，保存一次流程运行中的节点输出变量。
    /// </summary>
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

