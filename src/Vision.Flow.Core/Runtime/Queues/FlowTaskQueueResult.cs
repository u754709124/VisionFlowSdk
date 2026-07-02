namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 队列入队结果，表达任务是否被接受、拒绝、丢弃或仅通知。
    /// </summary>
    public class FlowTaskQueueResult
    {
        public bool IsAccepted { get; set; }

        public bool IsRejected { get; set; }

        public bool IsDropped { get; set; }

        public bool IsNotifyOnly { get; set; }

        public bool ShouldStopFlow { get; set; }

        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }
    }
}
