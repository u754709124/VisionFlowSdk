namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 表示不能安全保存在运行事件中的资源或大对象的轻量描述。
    /// </summary>
    public sealed class FlowRuntimeValueSummary
    {
        /// <summary>
        /// 获取或设置原始值的 CLR 类型名称。
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 获取或设置不持有原始对象的简短说明。
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 获取或设置可确定的字节数或集合元素数，未知时为 null。
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        /// 获取或设置原始值是否为需要显式释放的资源对象。
        /// </summary>
        public bool IsResource { get; set; }
    }
}
