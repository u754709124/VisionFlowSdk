using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
    public sealed class DefaultCameraFrameRouter : ICameraFrameRouter
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, CameraFrameBuffer> _buffers;
        private bool _isDisposed;

        public DefaultCameraFrameRouter()
        {
            _buffers = new Dictionary<string, CameraFrameBuffer>(StringComparer.OrdinalIgnoreCase);
            BufferedFrameTtl = TimeSpan.FromSeconds(30);
            MaxBufferedFrames = 64;
        }

        public TimeSpan BufferedFrameTtl { get; set; }

        public int MaxBufferedFrames { get; set; }

        public void EnsureCamera(ICameraAdapter camera, string cameraId)
        {
            GetBuffer(camera, cameraId);
        }

        public Task<CameraFrameData> WaitForFrameAsync(
            ICameraAdapter camera,
            CameraFrameWaitTicket ticket,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var buffer = GetBuffer(camera, ticket == null ? null : ticket.CameraId);
            var normalizedTicket = NormalizeTicket(ticket, buffer.CameraId, false);
            return buffer.WaitForFrameAsync(normalizedTicket, timeoutMs, cancellationToken);
        }

        public CameraFrameStreamSubscription Subscribe(ICameraAdapter camera, CameraFrameWaitTicket ticket)
        {
            var buffer = GetBuffer(camera, ticket == null ? null : ticket.CameraId);
            var normalizedTicket = NormalizeTicket(ticket, buffer.CameraId, true);
            return buffer.Subscribe(normalizedTicket);
        }

        public bool UnregisterCamera(string cameraId)
        {
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return false;
            }

            CameraFrameBuffer buffer = null;
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return false;
                }

                if (!_buffers.TryGetValue(cameraId, out buffer))
                {
                    return false;
                }

                _buffers.Remove(cameraId);
            }

            buffer.Dispose("Camera unregistered: " + cameraId);
            return true;
        }

        public void ClearExpiredFrames()
        {
            List<CameraFrameBuffer> buffers;
            lock (_gate)
            {
                buffers = new List<CameraFrameBuffer>(_buffers.Values);
            }

            for (var index = 0; index < buffers.Count; index++)
            {
                buffers[index].ClearExpiredFrames();
            }
        }

        public void CancelWaiters(string cameraId, string reason)
        {
            List<CameraFrameBuffer> buffers;
            lock (_gate)
            {
                if (string.IsNullOrWhiteSpace(cameraId))
                {
                    buffers = new List<CameraFrameBuffer>(_buffers.Values);
                }
                else
                {
                    CameraFrameBuffer buffer;
                    buffers = _buffers.TryGetValue(cameraId, out buffer)
                        ? new List<CameraFrameBuffer> { buffer }
                        : new List<CameraFrameBuffer>();
                }
            }

            for (var index = 0; index < buffers.Count; index++)
            {
                buffers[index].CancelWaiters(reason);
            }
        }

        public void Dispose()
        {
            List<CameraFrameBuffer> buffers;
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                buffers = new List<CameraFrameBuffer>(_buffers.Values);
                _buffers.Clear();
            }

            for (var index = 0; index < buffers.Count; index++)
            {
                buffers[index].Dispose("Camera frame router disposed.");
            }
        }

        private CameraFrameBuffer GetBuffer(ICameraAdapter camera, string cameraId)
        {
            if (camera == null)
            {
                throw new ArgumentNullException("camera");
            }

            var normalizedCameraId = NormalizeCameraId(camera, cameraId);
            lock (_gate)
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                CameraFrameBuffer buffer;
                if (_buffers.TryGetValue(normalizedCameraId, out buffer))
                {
                    if (!object.ReferenceEquals(buffer.Camera, camera))
                    {
                        throw new InvalidOperationException("A different camera adapter is already routed for CameraId: " + normalizedCameraId);
                    }

                    return buffer;
                }

                buffer = new CameraFrameBuffer(
                    normalizedCameraId,
                    camera,
                    GetSafeMaxBufferedFrames(),
                    GetSafeBufferedFrameTtl());
                _buffers[normalizedCameraId] = buffer;
                return buffer;
            }
        }

        private CameraFrameWaitTicket NormalizeTicket(CameraFrameWaitTicket ticket, string cameraId, bool stream)
        {
            var normalized = ticket == null ? new CameraFrameWaitTicket() : ticket.Clone();
            normalized.CameraId = string.IsNullOrWhiteSpace(normalized.CameraId) ? cameraId : normalized.CameraId;
            if (string.IsNullOrWhiteSpace(normalized.MatchMode))
            {
                normalized.MatchMode = CameraFrameMatchModes.TriggerId;
            }

            if (stream && !normalized.NotBeforeUtc.HasValue)
            {
                normalized.NotBeforeUtc = DateTime.UtcNow;
            }

            return normalized;
        }

        private static string NormalizeCameraId(ICameraAdapter camera, string cameraId)
        {
            var normalized = string.IsNullOrWhiteSpace(cameraId) ? camera.CameraId : cameraId;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("CameraId is required.", "cameraId");
            }

            if (!string.IsNullOrWhiteSpace(camera.CameraId) &&
                !string.Equals(camera.CameraId, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Camera adapter id does not match the requested CameraId: " + normalized);
            }

            return normalized;
        }

        private int GetSafeMaxBufferedFrames()
        {
            return MaxBufferedFrames <= 0 ? 1 : MaxBufferedFrames;
        }

        private TimeSpan GetSafeBufferedFrameTtl()
        {
            return BufferedFrameTtl <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : BufferedFrameTtl;
        }
    }
}
