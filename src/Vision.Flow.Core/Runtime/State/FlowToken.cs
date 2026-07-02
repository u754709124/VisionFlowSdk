using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vision.Flow.Core.Runtime.State
{
    /// <summary>
    /// ����ִ�� Token�����ص��ι�������λ����֡��ɨ�������ġ�
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
        /// ����Ψһ��ʶ�������¼��ͱ��������ʹ��������һ�����̴�����
        /// </summary>
        public string TokenId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public string ProductId { get; set; }

        public string WorkpieceId { get; set; }

        public string PositionId { get; set; }

        /// <summary>
        /// �ɼ��� ID�����ڶ��λͼ�����ϡ�
        /// </summary>
        public string CaptureGroupId { get; set; }

        /// <summary>
        /// ɨ���� ID����������ɨ��֡��ϡ�
        /// </summary>
        public string ScanGroupId { get; set; }

        public string FrameId { get; set; }

        /// <summary>
        /// ͨ��ҵ��Ԫ���ݣ����ڵ����λ�����ݷǹ̶��ֶΡ�
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// ���ƾֲ�ֵ����ʺ���ڴ���ʱע��ĳ�ʼ���ݡ�
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

