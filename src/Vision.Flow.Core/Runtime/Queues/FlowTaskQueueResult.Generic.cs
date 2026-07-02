namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 带返回值的队列执行结果。
    /// </summary>
    public sealed class FlowTaskQueueResult<T> : FlowTaskQueueResult
    {
        public T Value { get; set; }
    }
}
