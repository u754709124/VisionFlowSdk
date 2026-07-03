namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点设置键常量。设置键会写入流程文件并被属性面板、节点工厂和校验器共同读取。
    /// </summary>
    public static class FlowSettingNames
    {
        public const string CameraId = "CameraId";
        public const string TriggerId = "TriggerId";
        public const string TimeoutMs = "TimeoutMs";
        public const string JoinKeyBinding = "JoinKeyBinding";
        public const string ExpectedInputCount = "ExpectedInputCount";
        public const string DuplicatePolicy = "DuplicatePolicy";
        public const string LeftBinding = "LeftBinding";
        public const string Operator = "Operator";
        public const string RightValue = "RightValue";
        public const string RightBinding = "RightBinding";
        public const string DelayMs = "DelayMs";
        public const string Message = "Message";
        public const string Level = "Level";
        public const string VariableName = "VariableName";
        public const string Value = "Value";
        public const string ValueBinding = "ValueBinding";
        public const string ConstantValue = "ConstantValue";
        public const string Binding = "Binding";
        public const string Expression = "Expression";
        public const string Name = "Name";
        public const string ParameterName = "ParameterName";
        public const string Disabled = "Disabled";
    }
}
