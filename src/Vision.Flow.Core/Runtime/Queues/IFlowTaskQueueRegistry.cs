namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// ����ע���ӿڣ������ڶ���ڵ�֮�临�þ������ж��С�
    /// </summary>
    public interface IFlowTaskQueueRegistry
    {
        FlowTaskQueue GetOrCreate(string queueName, FlowTaskQueueOptions options = null);

        bool TryGetQueue(string queueName, out FlowTaskQueue queue);
    }
}
