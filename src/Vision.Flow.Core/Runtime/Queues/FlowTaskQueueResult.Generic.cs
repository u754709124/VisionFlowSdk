namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// ������ֵ�Ķ���ִ�н����
    /// </summary>
    public sealed class FlowTaskQueueResult<T> : FlowTaskQueueResult
    {
        public T Value { get; set; }
    }
}
