namespace Vision.Flow.Core.Services.Validation
{
    /// <summary>
    /// ��������У�����⣬�����ȶ�������Ͷ�λ�ֶΡ�
    /// </summary>
    public sealed class FlowValidationIssue
    {
        public FlowValidationSeverity Severity { get; set; }

        /// <summary>
        /// �ȶ������룬�ⲿ����Ӧ���������������������Ϣ�ı���
        /// </summary>
        public string Code { get; set; }

        public string Message { get; set; }

        public string NodeId { get; set; }

        public int? EdgeIndex { get; set; }

        public string EntryName { get; set; }

        /// <summary>
        /// �����ֶ�·����ͨ����Ӧ����̬�����еĽڵ㡢���ߡ���ڻ�����λ�á�
        /// </summary>
        public string Field { get; set; }
    }
}
