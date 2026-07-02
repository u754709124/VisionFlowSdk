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
    }
}
