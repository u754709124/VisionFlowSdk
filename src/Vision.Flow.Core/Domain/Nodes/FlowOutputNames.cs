namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点输出变量名常量。变量绑定表达式依赖这些名称定位上游节点输出。
    /// </summary>
    public static class FlowOutputNames
    {
        public const string Result = "Result";
        public const string Value = "Value";
        public const string VariableName = "VariableName";
        public const string DelayMs = "DelayMs";
        public const string IsMatched = "IsMatched";
        public const string JoinKey = "JoinKey";
        public const string ActualInputCount = "ActualInputCount";
        public const string ExpectedInputCount = "ExpectedInputCount";
        public const string Image = "Image";
        public const string ElapsedMs = "ElapsedMs";
    }
}
