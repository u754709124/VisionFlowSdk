using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// �˿��������ӿڣ���װ��λ���ȴ����˶���Ϣ֪ͨ������
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
