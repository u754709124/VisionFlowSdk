namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点设置键常量。设置键会写入流程文件并被属性面板、节点工厂和校验器共同读取。
    /// </summary>
    public static class FlowSettingNames
    {
        public const string TimeoutMs = "TimeoutMs";
        public const string JoinKeyBinding = "JoinKeyBinding";
        public const string ExpectedInputCount = "ExpectedInputCount";
        public const string DuplicatePolicy = "DuplicatePolicy";
        /// <summary>
        /// 条件判断节点左值设置键；该值必须保存为结构化变量绑定。
        /// </summary>
        public const string LeftValue = "LeftValue";
        public const string Operator = "Operator";
        public const string RightValue = "RightValue";
        public const string DelayMs = "DelayMs";
        public const string Message = "Message";
        public const string Level = "Level";
        public const string VariableName = "VariableName";
        public const string TargetScope = "TargetScope";
        public const string GlobalVariableId = "GlobalVariableId";
        public const string Value = "Value";
        public const string ValueBinding = "ValueBinding";
        public const string ConstantValue = "ConstantValue";
        public const string Binding = "Binding";
        public const string Expression = "Expression";
        public const string Name = "Name";
        public const string Disabled = "Disabled";
    }
}
