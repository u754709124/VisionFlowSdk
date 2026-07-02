namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 流程执行选项，由生产运行时或设计器调试运行传入。
    /// </summary>
    public sealed class FlowExecutionOptions
    {
        public FlowExecutionOptions()
        {
            FanOutMode = FlowFanOutMode.Sequential;
            MaxDegreeOfParallelism = 1;
            BranchTokenMode = FlowBranchTokenMode.Shared;
        }

        public FlowFanOutMode FanOutMode { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public FlowBranchTokenMode BranchTokenMode { get; set; }

        public bool ContinueOnBranchFailure { get; set; }

        public int DefaultNodeTimeoutMs { get; set; }
    }
}
