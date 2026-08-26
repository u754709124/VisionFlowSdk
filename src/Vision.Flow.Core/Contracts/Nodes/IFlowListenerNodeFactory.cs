namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// 标识创建监听节点的工厂，供设计器和静态校验在不实例化运行节点的情况下识别 NodeEvent 入口。
    /// </summary>
    public interface IFlowListenerNodeFactory : INodeFactory
    {
    }
}
