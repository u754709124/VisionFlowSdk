namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// �ڵ�˿�������Լ���˿���������Ϳ�����/�������͡�
    /// </summary>
    public sealed class NodePortDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Direction { get; set; }

        public string DataType { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }
}
