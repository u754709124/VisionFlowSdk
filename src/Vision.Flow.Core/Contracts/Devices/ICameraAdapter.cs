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

        /// <summary>
        /// 执行一次相机软触发并等待返回单帧图像；具体超时策略由调用方通过 CancellationToken 控制。
        /// </summary>
        Task<CameraFrameData> GrabOneAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// 相机收到外部硬触发并完成取像后触发；实现方应快速抛出事件，不在回调内执行重算法。
        /// </summary>
        event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;
    }
}
