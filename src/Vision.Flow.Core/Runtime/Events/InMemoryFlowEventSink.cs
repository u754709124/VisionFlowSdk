using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Runtime.Events
{
    public sealed class InMemoryFlowEventSink : IFlowEventSink
    {
        private readonly object _gate = new object();
        private readonly List<FlowRuntimeEvent> _events = new List<FlowRuntimeEvent>();

        public IList<FlowRuntimeEvent> Events
        {
            get
            {
                lock (_gate)
                {
                    return new List<FlowRuntimeEvent>(_events);
                }
            }
        }

        public Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            if (runtimeEvent == null)
            {
                throw new ArgumentNullException("runtimeEvent");
            }

            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                _events.Add(runtimeEvent);
            }

            return Task.FromResult(0);
        }
    }
}
