using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// ���������¼�ģ�ͣ����� Runtime �� UI����־��������λ������״̬�仯��
    /// </summary>
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

        /// <summary>
        /// �����¼����ݣ�����Ӧ����ʹ�� `FlowRuntimeDataKeys` ������
        /// </summary>
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
