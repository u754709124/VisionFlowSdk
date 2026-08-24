using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 丢弃全部运行事件的无状态出口，适合没有配置事件消费者的生产运行时。
    /// </summary>
    public sealed class NullFlowEventSink : IFlowEventSink
    {
        /// <summary>
        /// 获取共享的无状态事件出口实例。
        /// </summary>
        public static readonly NullFlowEventSink Instance = new NullFlowEventSink();

        private NullFlowEventSink()
        {
        }

        /// <summary>
        /// 接受事件但不保存或转发任何事件数据。
        /// </summary>
        public Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            if (runtimeEvent == null)
            {
                throw new ArgumentNullException("runtimeEvent");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }
    }
}
