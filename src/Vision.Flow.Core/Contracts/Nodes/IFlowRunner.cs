using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Contracts.Nodes
{
    public interface IFlowRunner
    {
        RuntimeFlowDefinition Definition { get; }

        /// <summary>获取当前 Runner 独占且由全部 FlowRun 和监听器共享的全局变量存储。</summary>
        IGlobalVariableStore GlobalVariables { get; }

        bool IsRunning { get; }

        Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

        Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

        Task<FlowRunResult> TriggerAsync(
            FlowTriggerRequest request,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
