namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 数据类型赋值兼容性；Warning 表示需要运行时检查或存在精度风险。
    /// </summary>
    public enum FlowDataTypeCompatibilityResult
    {
        Incompatible = 0,
        Compatible = 1,
        Warning = 2
    }

    /// <summary>
    /// 为校验器和设计器提供统一的节点输出到配置项类型兼容规则。
    /// </summary>
    public static class FlowDataTypeCompatibility
    {
        public static FlowDataTypeCompatibilityResult GetCompatibility(FlowDataType source, FlowDataType target)
        {
            if (source == FlowDataType.Control || target == FlowDataType.Control)
            {
                return FlowDataTypeCompatibilityResult.Incompatible;
            }

            if (source == target || target == FlowDataType.Object)
            {
                return FlowDataTypeCompatibilityResult.Compatible;
            }

            if (source == FlowDataType.Object)
            {
                return FlowDataTypeCompatibilityResult.Warning;
            }

            if ((source == FlowDataType.IVisionImage || source == FlowDataType.CameraFrameData) ||
                (target == FlowDataType.IVisionImage || target == FlowDataType.CameraFrameData))
            {
                return FlowDataTypeCompatibilityResult.Incompatible;
            }

            if (source == FlowDataType.Int32 && (target == FlowDataType.Int64 || target == FlowDataType.Double))
            {
                return FlowDataTypeCompatibilityResult.Compatible;
            }

            if (source == FlowDataType.Int64 && target == FlowDataType.Double)
            {
                return FlowDataTypeCompatibilityResult.Warning;
            }

            if (target == FlowDataType.String)
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
