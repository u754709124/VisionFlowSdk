using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 相机适配器接口，封装真实相机 SDK 或 Fake 相机的参数、软触发和帧回调能力。
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
