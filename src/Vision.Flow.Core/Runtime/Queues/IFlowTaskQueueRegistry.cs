namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 队列注册表接口，用于在多个节点之间复用具名运行队列。
    /// </summary>
    public interface IFlowTaskQueueRegistry
    {
        FlowTaskQueue GetOrCreate(string queueName, FlowTaskQueueOptions options = null);

        bool TryGetQueue(string queueName, out FlowTaskQueue queue);
    }
}
