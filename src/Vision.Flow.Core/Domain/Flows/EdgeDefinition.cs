namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// ���������߶��壬������������˿ں���������˿ڡ�
    /// </summary>
    public sealed class EdgeDefinition
    {
        public string FromNodeId { get; set; }

        public string FromPort { get; set; }

        public string ToNodeId { get; set; }

        public string ToPort { get; set; }
    }
}
