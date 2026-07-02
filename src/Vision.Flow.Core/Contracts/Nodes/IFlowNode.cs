using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// 运行时节点接口，所有公共节点通过该契约接入执行引擎。
    /// </summary>
    public interface IFlowNode
    {
        Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken);
    }
}
