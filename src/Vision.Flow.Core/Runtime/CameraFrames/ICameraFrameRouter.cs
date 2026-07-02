using System;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
    public interface ICameraFrameRouter : IDisposable
    {
        void EnsureCamera(ICameraAdapter camera, string cameraId);

        Task<CameraFrameData> WaitForFrameAsync(
            ICameraAdapter camera,
            CameraFrameWaitTicket ticket,
            int timeoutMs,
            CancellationToken cancellationToken);

        CameraFrameStreamSubscription Subscribe(
            ICameraAdapter camera,
            CameraFrameWaitTicket ticket);

        bool UnregisterCamera(string cameraId);

        void ClearExpiredFrames();

        void CancelWaiters(string cameraId, string reason);
    }
}
