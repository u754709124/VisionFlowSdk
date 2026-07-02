namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 分支 Token 处理模式，决定并行分支共享还是克隆运行上下文。
    /// </summary>
    public enum FlowBranchTokenMode
    {
        Shared = 0,
        Clone = 1
    }
}
