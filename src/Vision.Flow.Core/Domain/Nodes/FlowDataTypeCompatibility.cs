using System;

namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 数据类型赋值兼容性；Warning 为兼容保留值，严格配置绑定不再返回该状态。
    /// </summary>
    public enum FlowDataTypeCompatibilityResult
    {
        Incompatible = 0,
        Compatible = 1,
        Warning = 2
    }

    /// <summary>
    /// 为校验器和设计器提供统一的严格配置类型规则。
    /// </summary>
    public static class FlowDataTypeCompatibility
    {
        public static FlowDataTypeCompatibilityResult GetCompatibility(FlowDataType source, FlowDataType target)
        {
            if (source == FlowDataType.Control || target == FlowDataType.Control)
            {
                return FlowDataTypeCompatibilityResult.Incompatible;
            }

            if (source == target)
            {
                return FlowDataTypeCompatibilityResult.Compatible;
            }

            return FlowDataTypeCompatibilityResult.Incompatible;
        }

        public static bool IsCompatible(FlowDataType source, FlowDataType target)
        {
            return GetCompatibility(source, target) != FlowDataTypeCompatibilityResult.Incompatible;
        }

        /// <summary>
        /// 判断变量来源与目标设置的基础类型及具体枚举类型是否兼容。
        /// 只要任一侧声明枚举元数据，两侧就必须声明同一个有效枚举类型。
        /// </summary>
        public static bool IsCompatible(
            FlowDataType source,
            Type sourceEnumType,
            FlowDataType target,
            Type targetEnumType)
        {
            if (!IsCompatible(source, target))
            {
                return false;
            }

            if (sourceEnumType == null && targetEnumType == null)
            {
                return true;
            }

            return sourceEnumType != null &&
                targetEnumType != null &&
                sourceEnumType.IsEnum &&
                targetEnumType.IsEnum &&
                sourceEnumType == targetEnumType;
        }
    }
}
