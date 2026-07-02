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

namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 有界异步任务队列，用于让保存、算法等长耗时节点控制并发和背压。
    /// </summary>
    public sealed class FlowTaskQueue
    {
        private readonly SemaphoreSlim _capacity;
        private readonly SemaphoreSlim _workers;
        private readonly IFlowEventSink _eventSink;
        private int _depth;

        public FlowTaskQueue(FlowTaskQueueOptions options, IFlowEventSink eventSink = null)
        {
            if (options == null)
            {
                options = new FlowTaskQueueOptions();
            }

            QueueName = string.IsNullOrWhiteSpace(options.QueueName) ? FlowQueueNames.Default : options.QueueName;
            Capacity = options.Capacity <= 0 ? 1 : options.Capacity;
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism <= 0 ? 1 : options.MaxDegreeOfParallelism;
            FullMode = options.FullMode;
            _capacity = new SemaphoreSlim(Capacity, Capacity);
            _workers = new SemaphoreSlim(MaxDegreeOfParallelism, MaxDegreeOfParallelism);
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
        }

        public string QueueName { get; private set; }

        public int Capacity { get; private set; }

        public int MaxDegreeOfParallelism { get; private set; }

        public FlowTaskQueueFullMode FullMode { get; private set; }

        public int CurrentDepth
        {
            get { return Volatile.Read(ref _depth); }
        }

        public async Task<FlowTaskQueueResult> EnqueueAsync(
            Func<CancellationToken, Task> work,
            FlowTaskQueueItemContext context,
            CancellationToken cancellationToken)
        {
            if (work == null)
            {
                throw new ArgumentNullException("work");
            }

            var result = await EnqueueAsync<object>(
                async delegate(CancellationToken token)
                {
                    await work(token).ConfigureAwait(false);
                    return null;
                },
                context,
                cancellationToken).ConfigureAwait(false);

            return new FlowTaskQueueResult
            {
                IsAccepted = result.IsAccepted,
                IsRejected = result.IsRejected,
                IsDropped = result.IsDropped,
                IsNotifyOnly = result.IsNotifyOnly,
                ShouldStopFlow = result.ShouldStopFlow,
                IsSuccess = result.IsSuccess,
                ErrorMessage = result.ErrorMessage
            };
        }

        public async Task<FlowTaskQueueResult<T>> EnqueueAsync<T>(
            Func<CancellationToken, Task<T>> work,
            FlowTaskQueueItemContext context,
            CancellationToken cancellationToken)
        {
            if (work == null)
            {
                throw new ArgumentNullException("work");
            }

            context = NormalizeContext(context);
            var capacity = await TryAcquireCapacityAsync(context, cancellationToken).ConfigureAwait(false);
            if (capacity != QueueCapacityDecision.Acquired)
            {
                return CreateCapacityResult<T>(capacity);
            }

            try
            {
                await PublishAsync(FlowRuntimeEventType.QueueEnqueued, context, null, cancellationToken).ConfigureAwait(false);
                await _workers.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await PublishAsync(FlowRuntimeEventType.QueueStarted, context, null, cancellationToken).ConfigureAwait(false);
                    var value = await work(cancellationToken).ConfigureAwait(false);
                    await PublishAsync(FlowRuntimeEventType.QueueCompleted, context, null, cancellationToken).ConfigureAwait(false);
                    return new FlowTaskQueueResult<T>
                    {
                        IsAccepted = true,
                        IsRejected = false,
                        IsSuccess = true,
                        Value = value
                    };
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await PublishAsync(FlowRuntimeEventType.QueueFailed, context, ex.Message, CancellationToken.None).ConfigureAwait(false);
                    return new FlowTaskQueueResult<T>
                    {
                        IsAccepted = true,
                        IsRejected = false,
                        IsSuccess = false,
                        ErrorMessage = ex.Message
                    };
                }
                finally
                {
                    _workers.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _depth);
                _capacity.Release();
            }
        }

        public async Task<FlowTaskQueueResult> EnqueueDetachedAsync(
            Func<CancellationToken, Task> work,
            FlowTaskQueueItemContext context,
            CancellationToken cancellationToken)
        {
            if (work == null)
            {
                throw new ArgumentNullException("work");
            }

            var result = await EnqueueDetachedAsync<object>(
                async delegate(CancellationToken token)
                {
                    await work(token).ConfigureAwait(false);
                    return null;
                },
                context,
                cancellationToken).ConfigureAwait(false);

            return new FlowTaskQueueResult
            {
                IsAccepted = result.IsAccepted,
                IsRejected = result.IsRejected,
                IsDropped = result.IsDropped,
                IsNotifyOnly = result.IsNotifyOnly,
                ShouldStopFlow = result.ShouldStopFlow,
                IsSuccess = result.IsSuccess,
                ErrorMessage = result.ErrorMessage
            };
        }

        public async Task<FlowTaskQueueResult<T>> EnqueueDetachedAsync<T>(
            Func<CancellationToken, Task<T>> work,
            FlowTaskQueueItemContext context,
            CancellationToken cancellationToken)
        {
            if (work == null)
            {
                throw new ArgumentNullException("work");
            }

            context = NormalizeContext(context);
            var capacity = await TryAcquireCapacityAsync(context, cancellationToken).ConfigureAwait(false);
            if (capacity != QueueCapacityDecision.Acquired)
            {
                return CreateCapacityResult<T>(capacity);
            }

            try
            {
                await PublishAsync(FlowRuntimeEventType.QueueEnqueued, context, null, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Decrement(ref _depth);
                _capacity.Release();
                throw;
            }

            _ = Task.Run(delegate
            {
                return ExecuteDetachedWorkAsync(work, context);
            });

            return new FlowTaskQueueResult<T>
            {
                IsAccepted = true,
                IsRejected = false,
                IsSuccess = false
            };
        }

        private async Task ExecuteDetachedWorkAsync<T>(
            Func<CancellationToken, Task<T>> work,
            FlowTaskQueueItemContext context)
        {
            try
            {
                await _workers.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    await PublishAsync(FlowRuntimeEventType.QueueStarted, context, null, CancellationToken.None).ConfigureAwait(false);
                    await work(CancellationToken.None).ConfigureAwait(false);
                    await PublishAsync(FlowRuntimeEventType.QueueCompleted, context, null, CancellationToken.None).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    await PublishAsync(FlowRuntimeEventType.QueueFailed, context, ex.Message, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    await PublishAsync(FlowRuntimeEventType.QueueFailed, context, ex.Message, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _workers.Release();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _depth);
                _capacity.Release();
            }
        }

        private async Task<QueueCapacityDecision> TryAcquireCapacityAsync(FlowTaskQueueItemContext context, CancellationToken cancellationToken)
        {
            if (FullMode != FlowTaskQueueFullMode.Wait)
            {
                if (_capacity.Wait(0))
                {
                    Interlocked.Increment(ref _depth);
                    return QueueCapacityDecision.Acquired;
                }

                return await PublishFullQueueDecisionAsync(context, cancellationToken).ConfigureAwait(false);
            }

            await _capacity.WaitAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _depth);
            return QueueCapacityDecision.Acquired;
        }

        private async Task<QueueCapacityDecision> PublishFullQueueDecisionAsync(FlowTaskQueueItemContext context, CancellationToken cancellationToken)
        {
            var message = "Queue is full: " + QueueName;
            switch (FullMode)
            {
                case FlowTaskQueueFullMode.Drop:
                    await PublishAsync(FlowRuntimeEventType.QueueWarning, context, "Queue item dropped. " + message, cancellationToken).ConfigureAwait(false);
                    return QueueCapacityDecision.Dropped;
                case FlowTaskQueueFullMode.StopFlow:
                    await PublishAsync(FlowRuntimeEventType.QueueRejected, context, "Queue requested flow stop. " + message, cancellationToken).ConfigureAwait(false);
                    return QueueCapacityDecision.StopFlow;
                case FlowTaskQueueFullMode.NotifyOnly:
                    await PublishAsync(FlowRuntimeEventType.QueueWarning, context, "Queue full notification only. " + message, cancellationToken).ConfigureAwait(false);
                    return QueueCapacityDecision.NotifyOnly;
                default:
                    await PublishAsync(FlowRuntimeEventType.QueueRejected, context, message, cancellationToken).ConfigureAwait(false);
                    return QueueCapacityDecision.Rejected;
            }
        }

        private FlowTaskQueueResult<T> CreateCapacityResult<T>(QueueCapacityDecision capacity)
        {
            var result = new FlowTaskQueueResult<T>
            {
                IsAccepted = false,
                IsSuccess = false,
                ErrorMessage = "Queue is full: " + QueueName
            };

            if (capacity == QueueCapacityDecision.Dropped)
            {
                result.IsDropped = true;
                return result;
            }

            if (capacity == QueueCapacityDecision.NotifyOnly)
            {
                result.IsNotifyOnly = true;
                return result;
            }

            result.IsRejected = true;
            result.ShouldStopFlow = capacity == QueueCapacityDecision.StopFlow;
            return result;
        }

        private FlowTaskQueueItemContext NormalizeContext(FlowTaskQueueItemContext context)
        {
            context = context ?? new FlowTaskQueueItemContext();
            if (context.Data == null)
            {
                context.Data = new Dictionary<string, object>();
            }

            return context;
        }

        private Task PublishAsync(
            FlowRuntimeEventType eventType,
            FlowTaskQueueItemContext context,
            string message,
            CancellationToken cancellationToken)
        {
            var runtimeEvent = new FlowRuntimeEvent
            {
                EventType = eventType,
                FlowId = context.FlowId,
                TokenId = context.TokenId,
                NodeId = context.NodeId,
                NodeName = context.NodeName,
                Message = message,
                State = NodeRuntimeState.Running
            };
            runtimeEvent.Data[FlowRuntimeDataKeys.QueueName] = QueueName;
            runtimeEvent.Data[FlowRuntimeDataKeys.OperationName] = context.OperationName;
            runtimeEvent.Data[FlowRuntimeDataKeys.Depth] = CurrentDepth;
            runtimeEvent.Data[FlowRuntimeDataKeys.Capacity] = Capacity;
            runtimeEvent.Data[FlowRuntimeDataKeys.MaxDegreeOfParallelism] = MaxDegreeOfParallelism;
            runtimeEvent.Data[FlowRuntimeDataKeys.FullMode] = FullMode.ToString();

            foreach (var item in context.Data)
            {
                runtimeEvent.Data[item.Key] = item.Value;
            }

            return _eventSink.PublishAsync(runtimeEvent, cancellationToken);
        }

        private enum QueueCapacityDecision
        {
            Acquired = 0,
            Rejected = 1,
            Dropped = 2,
            StopFlow = 3,
            NotifyOnly = 4
        }
    }
}
