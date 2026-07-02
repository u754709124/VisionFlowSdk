using System.Collections.Generic;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// ��̨����ʽ�ڵ����������������������ָ������˿ڵ������ġ�
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
