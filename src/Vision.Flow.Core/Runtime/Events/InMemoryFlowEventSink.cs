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
