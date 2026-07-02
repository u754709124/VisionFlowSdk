using System;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Core.Runtime.Queues
{
    public sealed partial class FlowTaskQueue
    {
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
                async delegate (CancellationToken token)
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
    }
}
