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
        }

        public string SourceNodeId { get; set; }

        public string OutputPort { get; set; }

        public FlowToken Token { get; set; }

        public IVariablePool Variables { get; set; }

        public IDictionary<string, object> Outputs { get; set; }

        public string FlowRunId { get; set; }
    }
}
