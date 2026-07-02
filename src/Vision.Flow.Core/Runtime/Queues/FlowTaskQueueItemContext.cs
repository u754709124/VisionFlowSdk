using System.Collections.Generic;

namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 队列任务上下文，发布队列事件时用于定位流程、节点和 Token。
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
