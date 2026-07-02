using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 运控适配器接口，封装到位、等待和运动消息通知能力。
    /// </summary>
    public interface IMotionAdapter
    {
        string MotionId { get; }

        Task MoveToAsync(string positionName, CancellationToken cancellationToken);

        Task WaitForInPositionAsync(string positionName, CancellationToken cancellationToken);

        Task SendMessageAsync(MotionMessage message, CancellationToken cancellationToken);

        event EventHandler<MotionEventArgs> MotionEventReceived;
    }
}
