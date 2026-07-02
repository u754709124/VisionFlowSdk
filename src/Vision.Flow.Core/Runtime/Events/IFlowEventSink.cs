using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Runtime.Events
{
    public interface IFlowEventSink
    {
        Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken);
    }
}
