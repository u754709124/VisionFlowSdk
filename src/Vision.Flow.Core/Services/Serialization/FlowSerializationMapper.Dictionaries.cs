using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        private static Dictionary<string, object> ToObjectDictionary(object value)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            var dictionary = AsDictionaryOrNull(value);
            if (dictionary == null)
            {
                return result;
            }

            foreach (var item in dictionary)
            {
                result[item.Key] = NormalizeObject(item.Value);
            }

            return result;
        }
        private static IDictionary<string, object> AsDictionary(object value)
        {
            var dictionary = AsDictionaryOrNull(value);
            if (dictionary == null)
            {
                throw new InvalidOperationException("Expected a JSON object.");
            }

            return dictionary;
        }

        private static IDictionary<string, object> AsDictionaryOrNull(object value)
        {
            var typed = value as IDictionary<string, object>;
            if (typed != null)
            {
                return typed;
            }

            var dictionary = value as IDictionary;
            if (dictionary == null)
            {
                return null;
            }

            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in dictionary)
            {
                result[Convert.ToString(item.Key, CultureInfo.InvariantCulture)] = item.Value;
            }

            return result;
        }

        private static IEnumerable<object> AsEnumerable(object value)
        {
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                yield break;
            }

            foreach (var item in enumerable)
            {
                yield return item;
            }
        }

        private static bool TryGetValue(IDictionary<string, object> dictionary, string key, out object value)
        {
            value = null;
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
    }
}
