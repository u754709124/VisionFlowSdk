using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public sealed class AndJoinNodeConfig
    {
        public AndJoinNodeConfig()
        {
            ExpectedInputCount = 2;
            TimeoutMs = 0;
            DuplicatePolicy = FlowDuplicatePolicy.Ignore;
        }

        public string JoinKeyBinding { get; set; }

        public int ExpectedInputCount { get; set; }

        public int TimeoutMs { get; set; }

        public FlowDuplicatePolicy DuplicatePolicy { get; set; }
    }
}
