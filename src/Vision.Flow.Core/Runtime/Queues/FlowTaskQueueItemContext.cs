using System.Collections.Generic;

namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// �������������ģ����������¼�ʱ���ڶ�λ���̡��ڵ�� Token��
    /// </summary>
    public sealed class FlowTaskQueueItemContext
    {
        public string FlowId { get; set; }

        public string TokenId { get; set; }

        public string NodeId { get; set; }

        public string NodeName { get; set; }

        public string OperationName { get; set; }

        public IDictionary<string, object> Data { get; set; }
    }
}
