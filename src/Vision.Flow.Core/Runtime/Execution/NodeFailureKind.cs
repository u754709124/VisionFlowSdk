namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 节点失败分类，用于决定是否重试、选择失败策略并生成稳定运行事件。
    /// </summary>
    public enum NodeFailureKind
    {
        None = 0,
        Binding = 1,
        Configuration = 2,
        Execution = 3,
        Timeout = 4,
        Cancelled = 5
    }
}
