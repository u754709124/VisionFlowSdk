namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 控制流连线定义，连接上游输出端口和下游输入端口。
    /// </summary>
    public sealed class EdgeDefinition
    {
        public string FromNodeId { get; set; }

        public string FromPort { get; set; }

        public string ToNodeId { get; set; }

        public string ToPort { get; set; }
    }
}
