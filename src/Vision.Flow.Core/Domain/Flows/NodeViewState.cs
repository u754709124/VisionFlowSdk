namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 单个节点在设计器画布上的显示状态。
    /// </summary>
    public sealed class NodeViewState
    {
        public double X { get; set; }

        public double Y { get; set; }

        public bool IsCollapsed { get; set; }
    }
}
