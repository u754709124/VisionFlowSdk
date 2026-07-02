using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 流程运行事件模型，用于 Runtime 向 UI、日志或生产上位机发布状态变化。
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
        /// 附加事件数据，键名应优先使用 `FlowRuntimeDataKeys` 常量。
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
