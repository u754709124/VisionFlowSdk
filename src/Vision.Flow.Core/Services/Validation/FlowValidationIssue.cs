namespace Vision.Flow.Core.Services.Validation
{
    /// <summary>
    /// 单条流程校验问题，包含稳定错误码和定位字段。
    /// </summary>
    public sealed class FlowValidationIssue
    {
        public FlowValidationSeverity Severity { get; set; }

        /// <summary>
        /// 稳定错误码，外部工具应优先依赖错误码而不是消息文本。
        /// </summary>
        public string Code { get; set; }

        public string Message { get; set; }

        public string NodeId { get; set; }

        public int? EdgeIndex { get; set; }

        public string EntryName { get; set; }

        /// <summary>
        /// 问题字段路径，通常对应运行态定义中的节点、连线、入口或设置位置。
        /// </summary>
        public string Field { get; set; }
    }
}
