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
    }
}
