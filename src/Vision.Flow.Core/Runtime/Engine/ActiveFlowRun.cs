using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Runtime.Engine
{
    internal sealed class ActiveFlowRun
    {
        private readonly TaskCompletionSource<object> _completion =
            new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _terminalClaimed;

        public ActiveFlowRun(string flowRunId)
        {
            FlowRunId = flowRunId;
        }

        public string FlowRunId { get; private set; }

        public Task Completion
        {
            get { return _completion.Task; }
        }

        public bool TryClaimTerminal()
        {
            return Interlocked.CompareExchange(ref _terminalClaimed, 1, 0) == 0;
        }

        public void MarkCompleted()
        {
            _completion.TrySetResult(null);
        }
    }
}
