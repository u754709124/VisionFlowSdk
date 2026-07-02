using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Core.Runtime.Queues
{
    public sealed partial class FlowTaskQueue
    {
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
