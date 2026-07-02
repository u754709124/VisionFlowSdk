namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 队列满载时的处理策略，供长耗时算法或保存类节点使用。
    /// </summary>
    public enum FlowTaskQueueFullMode
    {
        Wait = 0,
        Reject = 1,
        Drop = 2,
        StopFlow = 3,
        NotifyOnly = 4
    }
}
