using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// ����ʱ�ڵ�ӿڣ����й����ڵ�ͨ������Լ����ִ�����档
    /// </summary>
    public interface IFlowNode
    {
        Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken);
    }
}
