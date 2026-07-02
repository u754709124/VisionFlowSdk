namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 设计态流程文档，包含可发布的运行态定义和设计器视图状态。
    /// </summary>
    public sealed class FlowDesignDocument
    {
        public FlowDesignDocument()
        {
            SchemaVersion = 1;
            Runtime = new RuntimeFlowDefinition();
            View = new FlowViewState();
        }

        /// <summary>
        /// 流程唯一标识，用于设计文件、运行文件和运行事件之间建立关联。
        /// </summary>
        public string FlowId { get; set; }

        /// <summary>
        /// 面向人的流程名称，不参与执行调度。
        /// </summary>
        public string FlowName { get; set; }

        /// <summary>
        /// 文件结构版本，用于后续兼容升级。
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// 可发布到生产环境的运行态定义。
        /// </summary>
        public RuntimeFlowDefinition Runtime { get; set; }

        /// <summary>
        /// 仅供设计器使用的画布状态，发布 `.flowruntime` 时必须移除。
        /// </summary>
        public FlowViewState View { get; set; }
    }
}
