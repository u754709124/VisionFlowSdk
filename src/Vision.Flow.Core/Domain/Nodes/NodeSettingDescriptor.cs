using System;

namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 校验节点配置项的单个常量值；返回空字符串表示校验通过。
    /// </summary>
    public delegate string NodeSettingValueValidator(object value);

    /// <summary>
    /// 校验变量来源的类型元数据；返回空字符串表示该变量类型可用于当前配置项。
    /// </summary>
    public delegate string NodeSettingVariableTypeValidator(
        FlowDataType dataType,
        Type enumType,
        Type objectType);

    /// <summary>
    /// 节点配置项描述，驱动设计器属性编辑和运行前校验。
    /// </summary>
    public sealed class NodeSettingDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public FlowDataType DataType { get; set; }

        /// <summary>
        /// 获取或设置字符串协议值对应的具体枚举类型；为空表示普通数据类型。
        /// 该元数据仅用于设计器候选和变量类型约束，不写入流程文件。
        /// </summary>
        public Type EnumType { get; set; }

        /// <summary>
        /// 获取或设置 Object 配置项要求的 CLR 契约类型；为空表示接受任意 Object。
        /// 该元数据只参与设计器候选与发布校验，不写入流程文件。
        /// </summary>
        public Type ObjectType { get; set; }

        public object DefaultValue { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }

        public NodeSettingBindingMode BindingMode { get; set; }

        public NodeSettingEvaluationPhase EvaluationPhase { get; set; }

        public VariableSelectorScopeFlags AllowedVariableSources { get; set; }

        /// <summary>
        /// 获取或设置设计器使用的专用编辑器；该元数据不写入流程文件。
        /// </summary>
        public NodeSettingEditorKind EditorKind { get; set; }

        /// <summary>
        /// 常量完成 DataType 转换后执行的同步单项校验器。
        /// 该委托属于 Descriptor 元数据，不会写入流程文件，也不会用于运行时变量值校验。
        /// </summary>
        public NodeSettingValueValidator Validator { get; set; }

        /// <summary>
        /// 获取或设置变量来源类型校验器；用于操作数等由所选变量决定实际类型的配置项。
        /// </summary>
        /// <remarks>该委托属于 Descriptor 元数据，不写入流程文件。</remarks>
        public NodeSettingVariableTypeValidator VariableTypeValidator { get; set; }

        /// <summary>
        /// 指示常量值变化后是否需要重新解析节点实例 Descriptor。
        /// </summary>
        /// <remarks>
        /// 该值属于设计器元数据，不写入 .flowdesign 或 .flowruntime。
        /// 影响 Descriptor 的配置项必须同时使用 ConstantOnly。
        /// </remarks>
        public bool AffectsDescriptor { get; set; }
    }

    /// <summary>
    /// 配置项是否允许从运行时变量取值。
    /// </summary>
    public enum NodeSettingBindingMode
    {
        ConstantOnly = 0,
        ConstantOrVariable = 1,

        /// <summary>配置项必须绑定执行期变量，不能使用固定值。</summary>
        VariableOnly = 2
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
        EnvironmentVariable = 8,
        GlobalVariable = 16,
        All = NodeOutput | TriggerInput | Token | EnvironmentVariable | GlobalVariable
    }

    /// <summary>节点设置在设计器属性面板中的编辑方式。</summary>
    public enum NodeSettingEditorKind
    {
        /// <summary>根据数据类型使用标准常量或变量编辑器。</summary>
        Standard = 0,

        /// <summary>使用“目标字段名 + 结构化变量来源”的有序表格编辑器。</summary>
        VariableSelectorMappings = 1
    }
}
