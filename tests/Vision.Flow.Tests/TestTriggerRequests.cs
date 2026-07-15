using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Tests
{
    internal static partial class FlowRunnerTests
    {
        private static FlowTriggerRequest CreateManualRequest(string entryName, FlowToken token)
        {
            return TestTriggerRequests.Manual(entryName, token);
        }
    }

    internal static partial class CommonNodeTests
    {
        private static FlowTriggerRequest CreateManualRequest(string entryName, FlowToken token)
        {
            return TestTriggerRequests.Manual(entryName, token);
        }
    }

    internal static partial class ControlFlowNodeTests
    {
        private static FlowTriggerRequest CreateManualRequest(string entryName, FlowToken token)
        {
            return TestTriggerRequests.Manual(entryName, token);
        }
    }

    internal static class TestTriggerRequests
    {
        public static FlowTriggerRequest Manual(string entryName, FlowToken token)
        {
            return new FlowTriggerRequest
            {
                EntryName = entryName,
                Source = FlowTriggerSource.Manual,
                Token = token
            };
        }
    }
}
