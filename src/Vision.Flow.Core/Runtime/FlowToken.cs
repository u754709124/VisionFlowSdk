using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vision.Flow.Core
{
    /// <summary>
    /// 流程执行 Token，承载单次工件、点位、组帧和扫描上下文。
    /// </summary>
    public sealed class FlowToken
    {
        public FlowToken()
        {
            TokenId = Guid.NewGuid().ToString("N");
            CreatedAtUtc = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
            Values = new Dictionary<string, object>();
        }

        /// <summary>
        /// 令牌唯一标识，运行事件和变量输出会使用它关联一次流程触发。
        /// </summary>
        public string TokenId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string ProductId { get; set; }

        public string WorkpieceId { get; set; }

        public string PositionId { get; set; }

        /// <summary>
        /// 采集组 ID，用于多点位图像组汇合。
        /// </summary>
        public string CaptureGroupId { get; set; }

        /// <summary>
        /// 扫描组 ID，用于连续扫描帧汇合。
        /// </summary>
        public string ScanGroupId { get; set; }

        public string FrameId { get; set; }

        /// <summary>
        /// 通用业务元数据，供节点和上位机传递非固定字段。
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// 令牌局部值表，适合入口触发时注入的初始数据。
        /// </summary>
        public Dictionary<string, object> Values { get; set; }

        public void Set(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Token value key is required.", "key");
            }

            Values[key] = value;
        }

        public object Get(string key)
        {
            object value;
            if (!TryGet(key, out value))
            {
                throw new KeyNotFoundException("Token value was not found: " + key);
            }

            return value;
        }

        public T Get<T>(string key)
        {
            return ConvertValue<T>(Get(key), key);
        }

        public bool TryGet(string key, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(key) || Values == null)
            {
                return false;
            }

            return Values.TryGetValue(key, out value);
        }

        public bool TryGet<T>(string key, out T value)
        {
            object rawValue;
            if (TryGet(key, out rawValue))
            {
                value = ConvertValue<T>(rawValue, key);
                return true;
            }

            value = default(T);
            return false;
        }

        private static T ConvertValue<T>(object value, string key)
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
                throw new InvalidCastException("Token value '" + key + "' cannot be converted to " + typeof(T).FullName + ".", ex);
            }
        }
    }
}
