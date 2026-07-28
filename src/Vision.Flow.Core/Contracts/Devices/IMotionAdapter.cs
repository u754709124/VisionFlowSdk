using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 运控适配器契约，通过逻辑命令名隔离具体设备协议和实现程序集。
    /// </summary>
    public interface IMotionAdapter
    {
        string MotionId { get; }

        Task<MotionAdapterCommandResult> SendCommandAsync(
            MotionAdapterCommandRequest request,
            CancellationToken cancellationToken);

        event EventHandler<MotionAdapterCommandReceivedEventArgs> CommandReceived;
    }
}
