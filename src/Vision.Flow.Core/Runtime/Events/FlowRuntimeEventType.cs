namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// ����ʱ�¼����ͣ�������λ����������������ͨ�����۲�����״̬��
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
        QueueWarning = 10,
        QueueEnqueued = 11,
        QueueStarted = 12,
        QueueCompleted = 13,
        QueueFailed = 14,
        QueueRejected = 15
    }
}
