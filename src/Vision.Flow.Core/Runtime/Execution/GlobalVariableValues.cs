using System;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>统一转换和校验全局变量默认值及运行写入值。</summary>
    public static class GlobalVariableValues
    {
        /// <summary>把输入转换为声明类型；String 允许空字符串，DateTime 保持原始强类型值且不做时区转换，所有类型均不允许 null。</summary>
        public static object ConvertValue(object value, FlowDataType dataType)
        {
            if (value == null)
            {
                throw new ArgumentException(
                    "Global variable values must not be null.",
                    "value");
            }

            switch (dataType)
            {
                case FlowDataType.String:
                    if (value is string)
                        return value;
                    break;
                case FlowDataType.Int32:
                    if (value is int)
                        return value;
                    break;
                case FlowDataType.Boolean:
                    if (value is bool)
                        return value;
                    break;
                case FlowDataType.DateTime:
                    if (value is DateTime)
                        return value;
                    break;
                default:
                    throw new ArgumentException(
                        "Global variable type is not supported: " + dataType,
                        "dataType");
            }

            throw new ArgumentException(
                "Global variable value cannot be converted to " + dataType + ".",
                "value");
        }
    }
}
