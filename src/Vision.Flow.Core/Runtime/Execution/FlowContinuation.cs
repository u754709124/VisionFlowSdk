using System.Collections.Generic;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 后台或流式节点向运行引擎请求继续调度指定输出端口的上下文。
    /// </summary>
    public sealed class FlowContinuation
    {
        public FlowContinuation()
        {
            OutputPort = FlowPortNames.Next;
            Outputs = new Dictionary<string, object>();
            TriggerInputs = new Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);
        }

        public string SourceNodeId { get; set; }

        public string OutputPort { get; set; }

        public FlowToken Token { get; set; }

        public IVariablePool Variables { get; set; }

        public IDictionary<string, object> Outputs { get; set; }

        public string FlowRunId { get; set; }

        /// <summary>
        /// NodeEvent 监听续流对应的入口名；普通节点续流保持为空。
        /// </summary>
        public string EntryName { get; set; }

        /// <summary>
        /// 与原流程运行共享的入口输入。
        /// </summary>
        public IDictionary<string, object> TriggerInputs { get; set; }
    }
}
