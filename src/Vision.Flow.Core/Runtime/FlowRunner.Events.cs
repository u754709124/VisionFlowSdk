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

namespace Vision.Flow.Core.Runtime
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
                BranchTokenMode = source.BranchTokenMode,
                ContinueOnBranchFailure = source.ContinueOnBranchFailure,
                DefaultNodeTimeoutMs = source.DefaultNodeTimeoutMs
            };
        }
    }
}
