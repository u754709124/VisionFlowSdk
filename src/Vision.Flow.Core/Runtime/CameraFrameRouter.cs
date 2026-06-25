using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core
{
    /// <summary>
    /// 相机帧匹配模式常量，用于相机回调节点和帧路由器之间协同过滤帧。
    /// </summary>
    public static class CameraFrameMatchModes
    {
        public const string TriggerId = "TriggerId";
        public const string Any = "Any";
        public const string TimeWindow = "TimeWindow";
        public const string ScanGroupId = "ScanGroupId";
    }

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

    public sealed class CameraFrameWaitTicket
    {
        public CameraFrameWaitTicket()
        {
            MatchMode = CameraFrameMatchModes.TriggerId;
        }

        public string CameraId { get; set; }

        public string MatchMode { get; set; }

        public string TriggerId { get; set; }

        public string ScanGroupId { get; set; }

        public DateTime? NotBeforeUtc { get; set; }

        public CameraFrameWaitTicket Clone()
        {
            return new CameraFrameWaitTicket
            {
                CameraId = CameraId,
                MatchMode = MatchMode,
                TriggerId = TriggerId,
                ScanGroupId = ScanGroupId,
                NotBeforeUtc = NotBeforeUtc
            };
        }

        public bool Matches(CameraFrameData frame)
        {
            if (frame == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(CameraId) &&
                !string.Equals(frame.CameraId, CameraId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (NotBeforeUtc.HasValue &&
                frame.GrabTime != default(DateTime) &&
                frame.GrabTime < NotBeforeUtc.Value)
            {
                return false;
            }

            var mode = string.IsNullOrWhiteSpace(MatchMode) ? CameraFrameMatchModes.TriggerId : MatchMode;
            if (string.Equals(mode, CameraFrameMatchModes.Any, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(mode, CameraFrameMatchModes.TriggerId, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(TriggerId) &&
                    string.Equals(frame.TriggerId, TriggerId, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(mode, CameraFrameMatchModes.ScanGroupId, StringComparison.OrdinalIgnoreCase))
            {
                var frameScanGroupId = GetMetadataString(frame, FlowMetadataKeys.ScanGroupId);
                return !string.IsNullOrWhiteSpace(ScanGroupId) &&
                    string.Equals(frameScanGroupId, ScanGroupId, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(mode, CameraFrameMatchModes.TimeWindow, StringComparison.OrdinalIgnoreCase))
            {
                return NotBeforeUtc.HasValue;
            }

            return false;
        }

        public string Describe()
        {
            var mode = string.IsNullOrWhiteSpace(MatchMode) ? CameraFrameMatchModes.TriggerId : MatchMode;
            if (string.Equals(mode, CameraFrameMatchModes.TriggerId, StringComparison.OrdinalIgnoreCase))
            {
                return "TriggerId=" + TriggerId;
            }

            if (string.Equals(mode, CameraFrameMatchModes.ScanGroupId, StringComparison.OrdinalIgnoreCase))
            {
                return "ScanGroupId=" + ScanGroupId;
            }

            return "MatchMode=" + mode;
        }

        private static string GetMetadataString(CameraFrameData frame, string name)
        {
            if (frame == null || frame.Metadata == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            object value;
            return frame.Metadata.TryGetValue(name, out value) ? Convert.ToString(value) : null;
        }
    }

    public sealed class CameraFrameStreamSubscription : IDisposable
    {
        private readonly CameraFrameBuffer _buffer;
        private readonly object _gate = new object();
        private bool _isDisposed;
        private int _deliveredCount;

        internal CameraFrameStreamSubscription(CameraFrameBuffer buffer, CameraFrameWaitTicket ticket)
        {
            _buffer = buffer ?? throw new ArgumentNullException("buffer");
            Ticket = ticket ?? throw new ArgumentNullException("ticket");
        }

        public event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;

        public string CameraId
        {
            get { return Ticket.CameraId; }
        }

        public CameraFrameWaitTicket Ticket { get; private set; }

        public string QueueName { get; set; }

        public int MaxFrameCount { get; set; }

        public string ScanGroupId { get; set; }

        internal bool IsDisposed
        {
            get
            {
                lock (_gate)
                {
                    return _isDisposed;
                }
            }
        }

        public Task<CameraFrameData> WaitForNextFrameAsync(int timeoutMs, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return _buffer.WaitForFrameAsync(Ticket, timeoutMs, cancellationToken);
        }

        public Task DispatchAsync(
            CameraFrameData frame,
            Func<CameraFrameData, CancellationToken, Task> dispatcher,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(
                delegate
                {
                    return dispatcher(frame, cancellationToken);
                },
                cancellationToken);
        }

        public void Dispose()
        {
            var shouldRemove = false;
            lock (_gate)
            {
                if (!_isDisposed)
                {
                    _isDisposed = true;
                    shouldRemove = true;
                }
            }

            if (shouldRemove)
            {
                _buffer.RemoveSubscription(this);
            }
        }

        internal void Notify(CameraFrameData frame)
        {
            EventHandler<CameraFrameArrivedEventArgs> handler;
            var shouldDisposeAfterDelivery = false;
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (MaxFrameCount > 0 && _deliveredCount >= MaxFrameCount)
                {
                    return;
                }

                _deliveredCount++;
                shouldDisposeAfterDelivery = MaxFrameCount > 0 && _deliveredCount >= MaxFrameCount;
                handler = FrameArrived;
            }

            if (handler != null)
            {
                handler(this, new CameraFrameArrivedEventArgs(frame));
            }

            if (shouldDisposeAfterDelivery)
            {
                Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }

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

    internal sealed class CameraFrameBuffer
    {
        private readonly object _gate = new object();
        private readonly List<BufferedCameraFrame> _frames;
        private readonly List<CameraFrameWaitRequest> _waiters;
        private readonly List<CameraFrameStreamSubscription> _subscriptions;
        private readonly int _maxBufferedFrames;
        private readonly TimeSpan _bufferedFrameTtl;
        private bool _isDisposed;

        public CameraFrameBuffer(
            string cameraId,
            ICameraAdapter camera,
            int maxBufferedFrames,
            TimeSpan bufferedFrameTtl)
        {
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                throw new ArgumentException("CameraId is required.", "cameraId");
            }

            if (camera == null)
            {
                throw new ArgumentNullException("camera");
            }

            CameraId = cameraId;
            Camera = camera;
            _maxBufferedFrames = maxBufferedFrames <= 0 ? 1 : maxBufferedFrames;
            _bufferedFrameTtl = bufferedFrameTtl <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : bufferedFrameTtl;
            _frames = new List<BufferedCameraFrame>();
            _waiters = new List<CameraFrameWaitRequest>();
            _subscriptions = new List<CameraFrameStreamSubscription>();
            camera.FrameArrived += OnFrameArrived;
        }

        public string CameraId { get; private set; }

        public ICameraAdapter Camera { get; private set; }

        public async Task<CameraFrameData> WaitForFrameAsync(
            CameraFrameWaitTicket ticket,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ticket == null)
            {
                throw new ArgumentNullException("ticket");
            }

            var request = new CameraFrameWaitRequest(ticket);
            lock (_gate)
            {
                ThrowIfDisposed();
                PruneExpiredFrames(DateTime.UtcNow);

                CameraFrameData bufferedFrame;
                if (TryTakeBufferedFrame(request, out bufferedFrame))
                {
                    return bufferedFrame;
                }

                _waiters.Add(request);
            }

            var timeoutTask = Task.Delay(timeoutMs > 0 ? timeoutMs : Timeout.Infinite, cancellationToken);
            await Task.WhenAny(request.Task, timeoutTask).ConfigureAwait(false);

            if (request.Task.IsCompleted)
            {
                return await request.Task.ConfigureAwait(false);
            }

            RemoveWaiter(request);
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        public CameraFrameStreamSubscription Subscribe(CameraFrameWaitTicket ticket)
        {
            if (ticket == null)
            {
                throw new ArgumentNullException("ticket");
            }

            var subscription = new CameraFrameStreamSubscription(this, ticket);
            lock (_gate)
            {
                ThrowIfDisposed();
                _subscriptions.Add(subscription);
            }

            return subscription;
        }

        public void RemoveSubscription(CameraFrameStreamSubscription subscription)
        {
            if (subscription == null)
            {
                return;
            }

            lock (_gate)
            {
                _subscriptions.Remove(subscription);
            }
        }

        public void ClearExpiredFrames()
        {
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                PruneExpiredFrames(DateTime.UtcNow);
            }
        }

        public void CancelWaiters(string reason)
        {
            List<CameraFrameWaitRequest> waiters;
            lock (_gate)
            {
                waiters = new List<CameraFrameWaitRequest>(_waiters);
                _waiters.Clear();
            }

            for (var index = 0; index < waiters.Count; index++)
            {
                waiters[index].TrySetCanceled(reason);
            }
        }

        public void Dispose(string reason)
        {
            List<CameraFrameWaitRequest> waiters;
            List<CameraFrameStreamSubscription> subscriptions;
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                Camera.FrameArrived -= OnFrameArrived;
                waiters = new List<CameraFrameWaitRequest>(_waiters);
                subscriptions = new List<CameraFrameStreamSubscription>(_subscriptions);
                _waiters.Clear();
                _subscriptions.Clear();
                _frames.Clear();
            }

            for (var index = 0; index < waiters.Count; index++)
            {
                waiters[index].TrySetCanceled(reason);
            }

            for (var index = 0; index < subscriptions.Count; index++)
            {
                subscriptions[index].Dispose();
            }
        }

        private void OnFrameArrived(object sender, CameraFrameArrivedEventArgs args)
        {
            if (args == null || args.Frame == null)
            {
                return;
            }

            CameraFrameWaitRequest completedRequest = null;
            var matchingSubscriptions = new List<CameraFrameStreamSubscription>();
            lock (_gate)
            {
                if (_isDisposed)
                {
                    return;
                }

                PruneExpiredFrames(DateTime.UtcNow);
                RemoveDisposedSubscriptions();

                for (var index = 0; index < _subscriptions.Count; index++)
                {
                    var subscription = _subscriptions[index];
                    if (!subscription.IsDisposed && subscription.Ticket.Matches(args.Frame))
                    {
                        matchingSubscriptions.Add(subscription);
                    }
                }

                for (var index = 0; index < _waiters.Count; index++)
                {
                    if (_waiters[index].Matches(args.Frame))
                    {
                        completedRequest = _waiters[index];
                        _waiters.RemoveAt(index);
                        break;
                    }
                }

                if (completedRequest == null)
                {
                    _frames.Add(new BufferedCameraFrame(args.Frame, DateTime.UtcNow));
                    while (_frames.Count > _maxBufferedFrames)
                    {
                        _frames.RemoveAt(0);
                    }
                }
            }

            if (completedRequest != null)
            {
                completedRequest.TrySetResult(args.Frame);
            }

            for (var index = 0; index < matchingSubscriptions.Count; index++)
            {
                matchingSubscriptions[index].Notify(args.Frame);
            }
        }

        private bool TryTakeBufferedFrame(CameraFrameWaitRequest request, out CameraFrameData frame)
        {
            for (var index = 0; index < _frames.Count; index++)
            {
                if (request.Matches(_frames[index].Frame))
                {
                    frame = _frames[index].Frame;
                    _frames.RemoveAt(index);
                    return true;
                }
            }

            frame = null;
            return false;
        }

        private void RemoveWaiter(CameraFrameWaitRequest request)
        {
            lock (_gate)
            {
                _waiters.Remove(request);
            }
        }

        private void PruneExpiredFrames(DateTime nowUtc)
        {
            for (var index = _frames.Count - 1; index >= 0; index--)
            {
                if (nowUtc - _frames[index].ArrivedUtc > _bufferedFrameTtl)
                {
                    _frames.RemoveAt(index);
                }
            }
        }

        private void RemoveDisposedSubscriptions()
        {
            for (var index = _subscriptions.Count - 1; index >= 0; index--)
            {
                if (_subscriptions[index].IsDisposed)
                {
                    _subscriptions.RemoveAt(index);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }

    internal sealed class CameraFrameWaitRequest
    {
        private readonly CameraFrameWaitTicket _ticket;
        private readonly TaskCompletionSource<CameraFrameData> _completion;

        public CameraFrameWaitRequest(CameraFrameWaitTicket ticket)
        {
            _ticket = ticket ?? throw new ArgumentNullException("ticket");
            _completion = new TaskCompletionSource<CameraFrameData>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<CameraFrameData> Task
        {
            get { return _completion.Task; }
        }

        public bool Matches(CameraFrameData frame)
        {
            return _ticket.Matches(frame);
        }

        public void TrySetResult(CameraFrameData frame)
        {
            _completion.TrySetResult(frame);
        }

        public void TrySetCanceled(string reason)
        {
            _completion.TrySetException(new OperationCanceledException(
                string.IsNullOrWhiteSpace(reason) ? "Camera frame wait was canceled." : reason));
        }
    }

    internal sealed class BufferedCameraFrame
    {
        public BufferedCameraFrame(CameraFrameData frame, DateTime arrivedUtc)
        {
            Frame = frame;
            ArrivedUtc = arrivedUtc;
        }

        public CameraFrameData Frame { get; private set; }

        public DateTime ArrivedUtc { get; private set; }
    }
}
