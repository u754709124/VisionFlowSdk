using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// 继续执行调度器接口，由流程运行器实现以支持流式逐帧输出。
    /// </summary>
    public interface IFlowContinuationDispatcher
    {
        Task DispatchAsync(FlowContinuation continuation, CancellationToken cancellationToken);
    }
}
