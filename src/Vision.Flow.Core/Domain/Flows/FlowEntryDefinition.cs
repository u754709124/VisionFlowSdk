namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// ������ڶ��壬����������λ�����ⲿ�¼�����ָ���ڵ㡣
    /// </summary>
    public sealed class FlowEntryDefinition
    {
        public string EntryName { get; set; }

        public string TargetNodeId { get; set; }
    }
}
