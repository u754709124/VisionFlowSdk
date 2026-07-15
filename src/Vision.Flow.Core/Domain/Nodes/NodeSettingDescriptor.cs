namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点配置项描述，驱动设计器属性编辑和运行前校验。
    /// </summary>
    public sealed class NodeSettingDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public FlowDataType DataType { get; set; }

        public object DefaultValue { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }

        public NodeSettingBindingMode BindingMode { get; set; }

        public NodeSettingEvaluationPhase EvaluationPhase { get; set; }

        public VariableSelectorScopeFlags AllowedVariableSources { get; set; }
    }

    /// <summary>
    /// 配置项是否允许从运行时变量取值。
    /// </summary>
    public enum NodeSettingBindingMode
    {
        ConstantOnly = 0,
        ConstantOrVariable = 1
    }

    /// <summary>
    /// 配置项被解析和使用的生命周期阶段。
    /// </summary>
    public enum NodeSettingEvaluationPhase
    {
        Execution = 0,
        ListenerStart = 1
    }

    /// <summary>
    /// 配置项允许选择的变量来源集合。
    /// </summary>
    [System.Flags]
    public enum VariableSelectorScopeFlags
    {
        None = 0,
        NodeOutput = 1,
        TriggerInput = 2,
        Token = 4,
        All = NodeOutput | TriggerInput | Token
    }
}
