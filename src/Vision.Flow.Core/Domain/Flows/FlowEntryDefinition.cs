namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 流程入口定义，用于生产上位机从外部事件触发指定节点。
    /// </summary>
    public sealed class FlowEntryDefinition
    {
        public string EntryName { get; set; }

        public string TargetNodeId { get; set; }
    }
}
