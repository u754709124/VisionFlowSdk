using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner
    {
        private Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            return _eventSink.PublishAsync(runtimeEvent, cancellationToken);
        }

        private FlowRuntimeEvent CreateRuntimeEvent(
            FlowRuntimeEventType eventType,
            FlowToken token,
            NodeDefinition node,
            NodeRuntimeState state,
            string message,
            string outputPort,
            string flowRunId,
            long elapsedMs)
        {
            var runtimeEvent = FlowRuntimeEvent.Create(
                eventType,
                _definition,
                token,
                node,
                state,
                message,
                outputPort);
            runtimeEvent.FlowRunId = flowRunId;
            runtimeEvent.ElapsedMs = elapsedMs;
            if (elapsedMs > 0)
            {
                runtimeEvent.Data[FlowRuntimeDataKeys.ElapsedMs] = elapsedMs;
            }

            return runtimeEvent;
        }

        private static FlowExecutionOptions CloneOptions(FlowExecutionOptions options)
        {
            var source = options ?? new FlowExecutionOptions();
            return new FlowExecutionOptions
            {
                FanOutMode = source.FanOutMode,
                MaxDegreeOfParallelism = source.MaxDegreeOfParallelism <= 0 ? 1 : source.MaxDegreeOfParallelism,
                DefaultNodeTimeoutMs = source.DefaultNodeTimeoutMs
            };
        }
    }
}
