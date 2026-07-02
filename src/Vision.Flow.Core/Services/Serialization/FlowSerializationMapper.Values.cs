using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        private static object NormalizeObject(object value)
        {
            var dictionary = AsDictionaryOrNull(value);
            if (dictionary != null)
            {
                return ToObjectDictionary(dictionary);
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(NormalizeObject(item));
                }

                return list;
            }

            return value;
        }
        private static object GetObject(IDictionary<string, object> dictionary, string key)
        {
            object value;
            return TryGetValue(dictionary, key, out value) ? NormalizeObject(value) : null;
        }

        private static string GetString(IDictionary<string, object> dictionary, string key)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;
        }

        private static int GetInt32(IDictionary<string, object> dictionary, string key, int defaultValue)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : defaultValue;
        }

        private static double GetDouble(IDictionary<string, object> dictionary, string key, double defaultValue)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : defaultValue;
        }

        private static bool GetBoolean(IDictionary<string, object> dictionary, string key, bool defaultValue)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToBoolean(value, CultureInfo.InvariantCulture)
                : defaultValue;
        }
    }
}
