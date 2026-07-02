using System;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
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
}
