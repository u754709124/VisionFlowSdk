using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ����������ӿڣ���װ��ʵ��� SDK �� Fake ����Ĳ������������֡�ص�������
    /// </summary>
    public interface ICameraAdapter
    {
        string CameraId { get; }

        IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors();

        Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken);

        Task<object> GetParameterAsync(string parameterName, CancellationToken cancellationToken);

        Task SoftTriggerAsync(CameraTriggerContext triggerContext, CancellationToken cancellationToken);

        event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;
    }
}
