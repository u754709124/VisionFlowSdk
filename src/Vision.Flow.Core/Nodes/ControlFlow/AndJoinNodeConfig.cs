namespace Vision.Flow.Nodes
{
    public sealed class AndJoinNodeConfig
    {
        public AndJoinNodeConfig()
        {
            ExpectedInputCount = 2;
            TimeoutMs = 0;
            DuplicatePolicy = "Ignore";
        }

        public string JoinKeyBinding { get; set; }

        public int ExpectedInputCount { get; set; }

        public int TimeoutMs { get; set; }

        public string DuplicatePolicy { get; set; }
    }
}
