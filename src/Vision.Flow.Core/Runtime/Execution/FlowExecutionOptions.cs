namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// ����ִ��ѡ�����������ʱ��������������д��롣
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
