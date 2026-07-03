using System;
using System.Collections.Generic;
using System.Globalization;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    internal static class CameraNodeHelpers
    {
        public static CameraFrameData CloneFrame(CameraFrameData frame)
        {
            if (frame == null)
            {
                return null;
            }

            var clone = new CameraFrameData
            {
                CameraId = frame.CameraId,
                TriggerId = frame.TriggerId,
                FrameId = frame.FrameId,
                GrabTime = frame.GrabTime,
                Image = frame.Image == null ? null : frame.Image.CloneReference()
            };
            CopyMetadata(frame.Metadata, clone.Metadata);
            return clone;
        }

        public static Dictionary<string, object> CreateFrameOutputs(CameraFrameData frame)
        {
            var outputs = new Dictionary<string, object>();
            if (frame == null)
            {
                return outputs;
            }

            outputs[FlowOutputNames.Frame] = frame;
            outputs[FlowOutputNames.Image] = frame.Image;
            outputs[FlowOutputNames.FrameId] = frame.FrameId;
            outputs[FlowOutputNames.GrabTime] = frame.GrabTime;
            outputs[FlowOutputNames.Metadata] = frame.Metadata;
            outputs[FlowOutputNames.CameraId] = frame.CameraId;
            outputs[FlowOutputNames.TriggerId] = frame.TriggerId;
            return outputs;
        }

        public static object ConvertParameterValue(object value, string valueType)
        {
            if (value == null || string.IsNullOrWhiteSpace(valueType))
            {
                return value;
            }

            if (string.Equals(valueType, "String", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (string.Equals(valueType, "Int32", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(valueType, "Int", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (string.Equals(valueType, "Int64", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(valueType, "Long", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            if (string.Equals(valueType, "Double", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(valueType, "Float", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (string.Equals(valueType, "Boolean", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(valueType, "Bool", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            return value;
        }

        private static void CopyMetadata(IDictionary<string, object> source, IDictionary<string, object> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (var item in source)
            {
                target[item.Key] = item.Value;
            }
        }
    }
}
