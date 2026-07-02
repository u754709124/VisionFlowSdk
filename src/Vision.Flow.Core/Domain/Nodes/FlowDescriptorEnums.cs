using System;
using System.Collections.Generic;
using System.Globalization;

namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点端口方向，用于约束设计器连线和节点描述符，不表示端口名称。
    /// </summary>
    public enum FlowPortDirection
    {
        Input = 0,
        Output = 1
    }

    /// <summary>
    /// 节点描述符中用于属性编辑和变量筛选的数据类型集合。
    /// </summary>
    public enum FlowDataType
    {
        Control = 0,
        String = 1,
        Int32 = 2,
        Int64 = 3,
        Boolean = 4,
        Double = 5,
        Object = 6,
        DateTime = 7,
        IVisionImage = 8,
        CameraFrameData = 9,
        RecipeRunResult = 10
    }

    /// <summary>
    /// 条件节点支持的比较操作符。
    /// </summary>
    public enum ConditionOperator
    {
        Equal = 0,
        NotEqual = 1,
        GreaterThan = 2,
        LessThan = 3,
        Contains = 4,
        IsNull = 5,
        IsNotNull = 6
    }

    /// <summary>
    /// AND Join 节点遇到同一 Token 重复输入时的处理策略。
    /// </summary>
    public enum FlowDuplicatePolicy
    {
        Error = 0,
        Ignore = 1,
        Replace = 2
    }

    /// <summary>
    /// 日志节点支持的日志等级。
    /// </summary>
    public enum FlowLogLevel
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// 统一枚举与流程文件字符串协议值之间的转换，避免枚举序号进入 .flowruntime。
    /// </summary>
    public static class FlowEnumConverter
    {
        public static string ToWireValue<TEnum>(TEnum value)
            where TEnum : struct
        {
            EnsureEnum<TEnum>();
            return value.ToString();
        }

        public static TEnum Parse<TEnum>(object value)
            where TEnum : struct
        {
            TEnum result;
            if (TryParse(value, out result))
            {
                return result;
            }

            throw new ArgumentException("Invalid enum wire value for " + typeof(TEnum).Name + ": " + Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        public static TEnum ParseOrDefault<TEnum>(object value, TEnum defaultValue)
            where TEnum : struct
        {
            TEnum result;
            return TryParse(value, out result) ? result : defaultValue;
        }

        public static bool TryParse<TEnum>(object value, out TEnum result)
            where TEnum : struct
        {
            EnsureEnum<TEnum>();
            result = default(TEnum);
            if (value == null)
            {
                return false;
            }

            if (value is TEnum)
            {
                result = (TEnum)value;
                return true;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text) || LooksNumeric(text))
            {
                return false;
            }

            if (!Enum.TryParse(text.Trim(), true, out result))
            {
                return false;
            }

            return Enum.IsDefined(typeof(TEnum), result);
        }

        public static string[] GetWireValues<TEnum>()
            where TEnum : struct
        {
            EnsureEnum<TEnum>();
            var names = Enum.GetNames(typeof(TEnum));
            var result = new List<string>(names.Length);
            for (var index = 0; index < names.Length; index++)
            {
                result.Add(names[index]);
            }

            return result.ToArray();
        }

        public static object NormalizeValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            var type = value.GetType();
            return type.IsEnum ? Convert.ToString(value, CultureInfo.InvariantCulture) : value;
        }

        private static void EnsureEnum<TEnum>()
            where TEnum : struct
        {
            if (!typeof(TEnum).IsEnum)
            {
                throw new InvalidOperationException(typeof(TEnum).FullName + " is not an enum type.");
            }
        }

        private static bool LooksNumeric(string value)
        {
            var text = value == null ? null : value.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var first = text[0];
            return char.IsDigit(first) || first == '-' || first == '+';
        }
    }
}
