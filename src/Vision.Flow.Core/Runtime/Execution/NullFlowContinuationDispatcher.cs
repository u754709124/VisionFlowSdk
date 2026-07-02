using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;

namespace Vision.Flow.Core.Runtime.Execution
{
    internal sealed class NullFlowContinuationDispatcher : IFlowContinuationDispatcher
    {
        public static readonly NullFlowContinuationDispatcher Instance = new NullFlowContinuationDispatcher();

        private NullFlowContinuationDispatcher()
        {
        }

        public Task DispatchAsync(FlowContinuation continuation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }
    }
}
