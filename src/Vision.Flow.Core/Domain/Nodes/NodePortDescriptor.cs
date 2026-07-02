namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点端口描述，约束端口名、方向和控制流/数据类型。
    /// </summary>
    public sealed class NodePortDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public FlowPortDirection Direction { get; set; }

        public FlowDataType DataType { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }
}
