namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// �ڵ�������������������������Ա༭������ǰУ�顣
    /// </summary>
    public sealed class NodeSettingDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string DataType { get; set; }

        public object DefaultValue { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }
}
