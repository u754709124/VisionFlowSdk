using System;
using System.Collections.Generic;
using System.Globalization;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner
    {
        private static bool TryPrepareTriggerInputs(
            FlowEntryDefinition entry,
            IDictionary<string, object> providedInputs,
            out IDictionary<string, object> effectiveInputs,
            out string errorMessage)
        {
            effectiveInputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            errorMessage = null;
            var descriptors = entry.Inputs ?? new List<TriggerInputDescriptor>();
            var descriptorMap = new Dictionary<string, TriggerInputDescriptor>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                if (descriptor != null && !string.IsNullOrWhiteSpace(descriptor.Name) && !descriptorMap.ContainsKey(descriptor.Name))
                {
                    descriptorMap[descriptor.Name] = descriptor;
                }
            }

            if (providedInputs != null)
            {
                foreach (var item in providedInputs)
                {
                    if (!descriptorMap.ContainsKey(item.Key))
                    {
                        errorMessage = "Trigger input is not declared by entry '" + entry.EntryName + "': " + item.Key;
                        return false;
                    }
                }
            }

            foreach (var item in descriptorMap)
            {
                var descriptor = item.Value;
                object rawValue;
                if (!TryGetIgnoreCase(providedInputs, descriptor.Name, out rawValue))
                {
                    rawValue = descriptor.DefaultValue;
                    if (rawValue == null && descriptor.IsRequired)
                    {
                        errorMessage = "Required trigger input is missing: " + descriptor.Name;
                        return false;
                    }
                }

                if (rawValue == null)
                {
                    effectiveInputs[descriptor.Name] = null;
                    continue;
                }

                object normalizedValue;
                if (!TryNormalizeTriggerInputValue(rawValue, descriptor.DataType, out normalizedValue))
                {
                    errorMessage = "Trigger input '" + descriptor.Name + "' cannot be converted to " + descriptor.DataType + ".";
                    return false;
                }

                effectiveInputs[descriptor.Name] = normalizedValue;
            }

            return true;
        }

        private static bool TryNormalizeTriggerInputValue(object value, FlowDataType dataType, out object normalizedValue)
        {
            normalizedValue = value;
            try
            {
                switch (dataType)
                {
                    case FlowDataType.String:
                        normalizedValue = Convert.ToString(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Int32:
                        normalizedValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Int64:
                        normalizedValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Boolean:
                        normalizedValue = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Double:
                        normalizedValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.DateTime:
                        normalizedValue = Convert.ToDateTime(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.IVisionImage:
                        return value is IVisionImage;
                    case FlowDataType.CameraFrameData:
                        return value is CameraFrameData;
                    case FlowDataType.Object:
                        return true;
                    case FlowDataType.Control:
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                normalizedValue = null;
                return false;
            }
        }

        private static bool TryGetIgnoreCase(
            IDictionary<string, object> dictionary,
            string key,
            out object value)
        {
            value = null;
            if (dictionary == null)
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
