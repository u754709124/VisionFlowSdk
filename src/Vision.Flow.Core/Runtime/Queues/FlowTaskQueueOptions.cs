namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// ��������������ã����������������Ⱥ����ز��ԡ�
    /// </summary>
    public sealed class FlowTaskQueueOptions
    {
        public FlowTaskQueueOptions()
        {
            QueueName = FlowQueueNames.Default;
            Capacity = 16;
            MaxDegreeOfParallelism = 1;
            FullMode = FlowTaskQueueFullMode.Wait;
        }

        public string QueueName { get; set; }

        public int Capacity { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public FlowTaskQueueFullMode FullMode { get; set; }
    }
}
