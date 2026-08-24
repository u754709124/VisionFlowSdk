using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 通过有界队列异步转发运行事件，关键生命周期事件不丢弃，普通遥测按配置处理溢出。
    /// </summary>
    public sealed class BoundedFlowEventSink : IFlowEventSink, IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<FlowRuntimeEvent> _queue = new Queue<FlowRuntimeEvent>();
        private readonly IFlowEventSink _downstream;
        private readonly FlowEventSinkOptions _options;
        private TaskCompletionSource<object> _spaceAvailable;
        private Task _drainTask = Task.FromResult(0);
        private bool _drainScheduled;
        private bool _disposed;
        private long _droppedEventCount;
        private long _publishedEventCount;
        private long _faultedEventCount;
        private Exception _lastError;

        /// <summary>
        /// 创建使用空下游的生产安全默认出口；事件会异步消费但不会持久保存。
        /// </summary>
        public BoundedFlowEventSink()
            : this(NullFlowEventSink.Instance, null)
        {
        }

        /// <summary>
        /// 创建向指定下游异步转发的有界事件出口；该实例不拥有下游出口。
        /// </summary>
        public BoundedFlowEventSink(IFlowEventSink downstream, FlowEventSinkOptions options = null)
        {
            _downstream = downstream ?? throw new ArgumentNullException("downstream");
            _options = options ?? new FlowEventSinkOptions();
            if (_options.Capacity <= 0)
            {
                throw new ArgumentOutOfRangeException("options", "Event sink capacity must be greater than zero.");
            }
        }

        /// <summary>
        /// 获取因队列溢出而丢弃的非关键遥测事件总数。
        /// </summary>
        public long DroppedEventCount
        {
            get { return Interlocked.Read(ref _droppedEventCount); }
        }

        /// <summary>
        /// 获取已成功交给下游出口的事件总数。
        /// </summary>
        public long PublishedEventCount
        {
            get { return Interlocked.Read(ref _publishedEventCount); }
        }

        /// <summary>
        /// 获取下游出口处理失败的事件总数。
        /// </summary>
        public long FaultedEventCount
        {
            get { return Interlocked.Read(ref _faultedEventCount); }
        }

        /// <summary>
        /// 获取当前排队等待转发的事件数量。
        /// </summary>
        public int QueuedEventCount
        {
            get
            {
                lock (_gate)
                {
                    return _queue.Count;
                }
            }
        }

        /// <summary>
        /// 获取最近一次下游出口异常；没有异常时为 null。
        /// </summary>
        public Exception LastError
        {
            get
            {
                lock (_gate)
                {
                    return _lastError;
                }
            }
        }

        /// <summary>
        /// 将事件加入有界队列；关键事件在满载时异步等待，普通遥测遵循溢出策略。
        /// </summary>
        public async Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            if (runtimeEvent == null)
            {
                throw new ArgumentNullException("runtimeEvent");
            }

            while (true)
            {
                Task waitTask = null;
                lock (_gate)
                {
                    ThrowIfDisposed();
                    if (_queue.Count < _options.Capacity)
                    {
                        _queue.Enqueue(runtimeEvent);
                        EnsureDrainScheduled();
                        return;
                    }

                    var mustWait = IsCritical(runtimeEvent.EventType) ||
                        _options.OverflowPolicy == FlowEventOverflowPolicy.Wait;
                    if (!mustWait)
                    {
                        if (_options.OverflowPolicy == FlowEventOverflowPolicy.DropOldest &&
                            TryRemoveOldestTelemetry())
                        {
                            Interlocked.Increment(ref _droppedEventCount);
                            _queue.Enqueue(runtimeEvent);
                            EnsureDrainScheduled();
                            return;
                        }

                        Interlocked.Increment(ref _droppedEventCount);
                        return;
                    }

                    if (_spaceAvailable == null || _spaceAvailable.Task.IsCompleted)
                    {
                        _spaceAvailable = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                    }

                    waitTask = _spaceAvailable.Task;
                    EnsureDrainScheduled();
                }

                await WaitWithCancellationAsync(waitTask, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 异步等待调用时已经排队的事件完成下游转发。
        /// </summary>
        public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            while (true)
            {
                Task drainTask;
                lock (_gate)
                {
                    if (_queue.Count == 0 && !_drainScheduled)
                    {
                        return;
                    }

                    drainTask = _drainTask;
                }

                await WaitWithCancellationAsync(drainTask, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 停止接受新事件并释放尚未转发的队列；调用方应先调用 FlushAsync 完成优雅排空。
        /// </summary>
        public void Dispose()
        {
            TaskCompletionSource<object> waiter;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _queue.Clear();
                waiter = _spaceAvailable;
                _spaceAvailable = null;
            }

            waiter?.TrySetException(new ObjectDisposedException(GetType().FullName));
        }

        private void EnsureDrainScheduled()
        {
            if (_drainScheduled)
            {
                return;
            }

            _drainScheduled = true;
            _drainTask = Task.Run((Func<Task>)DrainAsync);
        }

        private async Task DrainAsync()
        {
            while (true)
            {
                FlowRuntimeEvent runtimeEvent;
                TaskCompletionSource<object> waiter;
                lock (_gate)
                {
                    if (_disposed || _queue.Count == 0)
                    {
                        _drainScheduled = false;
                        return;
                    }

                    runtimeEvent = _queue.Dequeue();
                    waiter = _spaceAvailable;
                    _spaceAvailable = null;
                }

                waiter?.TrySetResult(null);
                try
                {
                    await _downstream.PublishAsync(runtimeEvent, CancellationToken.None).ConfigureAwait(false);
                    Interlocked.Increment(ref _publishedEventCount);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _faultedEventCount);
                    lock (_gate)
                    {
                        _lastError = ex;
                    }
                }
            }
        }

        private bool TryRemoveOldestTelemetry()
        {
            var removed = false;
            var count = _queue.Count;
            for (var index = 0; index < count; index++)
            {
                var queued = _queue.Dequeue();
                if (!removed && !IsCritical(queued.EventType))
                {
                    removed = true;
                    continue;
                }

                _queue.Enqueue(queued);
            }

            return removed;
        }

        private static bool IsCritical(FlowRuntimeEventType eventType)
        {
            return eventType == FlowRuntimeEventType.FlowStarted ||
                eventType == FlowRuntimeEventType.FlowStopped ||
                eventType == FlowRuntimeEventType.TokenCreated ||
                eventType == FlowRuntimeEventType.FlowRunStarted ||
                eventType == FlowRuntimeEventType.FlowRunCompleted ||
                eventType == FlowRuntimeEventType.FlowRunFailed ||
                eventType == FlowRuntimeEventType.FlowRunCancelled ||
                eventType == FlowRuntimeEventType.FlowRunRejected ||
                eventType == FlowRuntimeEventType.NodeFailed ||
                eventType == FlowRuntimeEventType.NodeTimeout ||
                eventType == FlowRuntimeEventType.NodeCancelled;
        }

        private static async Task WaitWithCancellationAsync(Task task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            var cancellation = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancellation.TrySetCanceled()))
            {
                await (await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
