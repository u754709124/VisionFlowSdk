namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点输出变量描述，供变量选择器和绑定校验使用。
    /// </summary>
    public sealed class NodeOutputDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string DataType { get; set; }

        public string Description { get; set; }
    }
}
