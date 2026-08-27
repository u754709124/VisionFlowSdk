using System;
using System.Globalization;
using Vision.Flow.Core.Contracts.Devices;

namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 为发布校验和设计器提供统一的节点配置常量类型转换与单项校验。
    /// </summary>
    public static class NodeSettingValueValidation
    {
        /// <summary>
        /// 校验变量来源类型是否满足配置项的固定类型或自定义类型约束。
        /// </summary>
        public static bool TryValidateVariableType(
            NodeSettingDescriptor descriptor,
            FlowDataType dataType,
            Type enumType,
            Type objectType,
            out string error)
        {
            error = null;
            if (descriptor == null)
            {
                error = "Setting descriptor is required.";
                return false;
            }

            if (descriptor.VariableTypeValidator != null)
            {
                try
                {
                    error = descriptor.VariableTypeValidator(
                        dataType,
                        enumType,
                        objectType);
                    return string.IsNullOrWhiteSpace(error);
                }
                catch (Exception ex)
                {
                    error = "Custom variable type validator failed: " + ex.Message;
                    return false;
                }
            }

            if (FlowDataTypeCompatibility.IsCompatible(
                dataType,
                enumType,
                objectType,
                descriptor.DataType,
                descriptor.EnumType,
                descriptor.ObjectType))
            {
                return true;
            }

            error = "Variable type is incompatible with setting type.";
            return false;
        }

        public static bool TryValidateConstant(
            NodeSettingDescriptor descriptor,
            object value,
            out object normalizedValue,
            out string error)
        {
            normalizedValue = null;
            error = null;
            if (descriptor == null)
            {
                error = "Setting descriptor is required.";
                return false;
            }

            if (!Enum.IsDefined(typeof(FlowDataType), descriptor.DataType) ||
                descriptor.DataType == FlowDataType.Control)
            {
                error = "Setting data type is invalid: " + descriptor.DataType + ".";
                return false;
            }

            if (value == null)
            {
                return true;
            }

            if (!TryNormalizeValue(value, descriptor.DataType, out normalizedValue))
            {
                error = "Value cannot be converted to " + descriptor.DataType + ".";
                return false;
            }

            if (descriptor.Validator == null)
            {
                return true;
            }

            try
            {
                error = descriptor.Validator(normalizedValue);
                return string.IsNullOrWhiteSpace(error);
            }
            catch (Exception ex)
            {
                error = "Custom setting validator failed: " + ex.Message;
                return false;
            }
        }

        private static bool TryNormalizeValue(
            object value,
            FlowDataType dataType,
            out object normalizedValue)
        {
            normalizedValue = null;
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            switch (dataType)
            {
                case FlowDataType.String:
                    if (value is string)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    return false;
                case FlowDataType.Int32:
                    int intValue;
                    if (value is int)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                    {
                        normalizedValue = intValue;
                        return true;
                    }
                    return false;
                case FlowDataType.Int64:
                    long longValue;
                    if (value is long)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue))
                    {
                        normalizedValue = longValue;
                        return true;
                    }
                    return false;
                case FlowDataType.Boolean:
                    bool boolValue;
                    if (value is bool)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    if (bool.TryParse(text, out boolValue))
                    {
                        normalizedValue = boolValue;
                        return true;
                    }
                    return false;
                case FlowDataType.Double:
                    double doubleValue;
                    if (value is double)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue))
                    {
                        normalizedValue = doubleValue;
                        return true;
                    }
                    return false;
                case FlowDataType.DateTime:
                    DateTime dateTime;
                    if (value is DateTime)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    if (DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out dateTime))
                    {
                        normalizedValue = dateTime;
                        return true;
                    }
                    return false;
                case FlowDataType.IVisionImage:
                    if (value is IVisionImage)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    return false;
                case FlowDataType.CameraFrameData:
                    if (value is CameraFrameData)
                    {
                        normalizedValue = value;
                        return true;
                    }
                    return false;
                case FlowDataType.Object:
                    normalizedValue = value;
                    return true;
                default:
                    return false;
            }
        }
    }
}
