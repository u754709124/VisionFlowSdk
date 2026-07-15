using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Contracts.Nodes
{
    public interface IFlowRunner
    {
        RuntimeFlowDefinition Definition { get; }

        bool IsRunning { get; }

        Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

        Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

        Task<FlowRunResult> TriggerAsync(
            FlowTriggerRequest request,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
