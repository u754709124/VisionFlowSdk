using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 定义流程 Session 内可变全局变量的稳定协议、类型和初始默认值。
    /// </summary>
    public sealed class GlobalVariableDefinition
    {
        /// <summary>获取或设置不随显示名称变化的稳定变量标识。</summary>
        public string Id { get; set; }

        /// <summary>获取或设置面向流程设计者的变量名称。</summary>
        public string Name { get; set; }

        /// <summary>获取或设置变量运行值必须遵守的数据类型。</summary>
        public FlowDataType DataType { get; set; }

        /// <summary>获取或设置每次创建新 Runner 时使用的初始值。</summary>
        public object DefaultValue { get; set; }
    }
}
