namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// ��������ʱ�Ĵ�����ԣ�������ʱ�㷨�򱣴���ڵ�ʹ�á�
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
