namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// ������ӽ������������Ƿ񱻽��ܡ��ܾ����������֪ͨ��
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
