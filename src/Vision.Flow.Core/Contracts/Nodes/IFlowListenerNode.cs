using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// 运行监听节点契约，用于流程启动时订阅外部事件，停止时释放订阅资源。
    /// </summary>
    public interface IFlowListenerNode : IFlowNode
    {
        /// <summary>
        /// 流程启动时调用，用于订阅外部事件或打开轻量监听资源。
        /// </summary>
        Task StartAsync(FlowListenerContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 流程停止时调用，用于取消订阅并释放监听资源。
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken);
    }
}
