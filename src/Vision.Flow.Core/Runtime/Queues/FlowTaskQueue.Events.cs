using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Core.Runtime.Queues
{
    public sealed partial class FlowTaskQueue
    {
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
    }
}
