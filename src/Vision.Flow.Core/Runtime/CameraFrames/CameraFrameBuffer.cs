using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
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
}
