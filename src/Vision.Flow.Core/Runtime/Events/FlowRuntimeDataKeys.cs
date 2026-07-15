namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 运行事件 Data 字典键常量，供 Runtime、Demo 和测试读取运行时事件负载。
    /// </summary>
    public static class FlowRuntimeDataKeys
    {
        public const string VariableName = "VariableName";
        public const string Value = "Value";
        public const string Depth = "Depth";
        public const string Capacity = "Capacity";
        public const string MaxDegreeOfParallelism = "MaxDegreeOfParallelism";
        public const string FullMode = "FullMode";
        public const string TokenId = "TokenId";
        public const string NodeId = "NodeId";
        public const string NodeName = "NodeName";
        public const string OperationName = "OperationName";
        public const string FlowId = "FlowId";
        public const string ElapsedMs = "ElapsedMs";
        public const string Kind = "Kind";
        public const string LogLevel = "LogLevel";
        public const string Message = "Message";
        public const string EntryName = "EntryName";
        public const string TriggerSource = "TriggerSource";
        public const string TriggerInputs = "TriggerInputs";
        public const string FlowRunStatus = "FlowRunStatus";
        public const string Attempt = "Attempt";
        public const string FailureKind = "FailureKind";
        public const string FailureStrategy = "FailureStrategy";
    }
}
