namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 运行任务队列配置，控制容量、并发度和满载策略。
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
