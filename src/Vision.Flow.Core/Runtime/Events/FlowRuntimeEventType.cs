namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 运行时事件类型，生产上位机和设计器调试面板通过它观察流程状态。
    /// </summary>
    public enum FlowRuntimeEventType
    {
        FlowStarted = 0,
        FlowStopped = 1,
        TokenCreated = 2,
        NodeWaiting = 3,
        NodeStarted = 4,
        NodeCompleted = 5,
        NodeFailed = 6,
        NodeTimeout = 7,
        OutputProduced = 8,
        ImageProduced = 9,
        FlowRunStarted = 10,
        FlowRunCompleted = 11,
        FlowRunFailed = 12,
        FlowRunCancelled = 13,
        FlowRunRejected = 14
    }
}
