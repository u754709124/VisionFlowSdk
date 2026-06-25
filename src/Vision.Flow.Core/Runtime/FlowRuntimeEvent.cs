using System;
using System.Collections.Generic;

namespace Vision.Flow.Core
{
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

    public enum NodeRuntimeState
    {
        Waiting = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Timeout = 4,
        Stopped = 5
    }

    public sealed class FlowRuntimeEvent
    {
        public FlowRuntimeEvent()
        {
            TimestampUtc = DateTime.UtcNow;
            Data = new Dictionary<string, object>();
        }

        public DateTime TimestampUtc { get; set; }

        public FlowRuntimeEventType EventType { get; set; }

        public string FlowId { get; set; }

        public string FlowRunId { get; set; }

        public string TokenId { get; set; }

        public string NodeId { get; set; }

        public string NodeName { get; set; }

        public NodeRuntimeState State { get; set; }

        public string OutputPort { get; set; }

        public string Message { get; set; }

        public long ElapsedMs { get; set; }

        public Dictionary<string, object> Data { get; set; }

        public static FlowRuntimeEvent Create(
            FlowRuntimeEventType eventType,
            RuntimeFlowDefinition flow,
            FlowToken token,
            NodeDefinition node = null,
            NodeRuntimeState state = NodeRuntimeState.Waiting,
            string message = null,
            string outputPort = null)
        {
            return new FlowRuntimeEvent
            {
                EventType = eventType,
                FlowId = flow == null ? null : flow.FlowId,
                TokenId = token == null ? null : token.TokenId,
                NodeId = node == null ? null : node.Id,
                NodeName = node == null ? null : node.Name,
                State = state,
                Message = message,
                OutputPort = outputPort
            };
        }
    }
}
