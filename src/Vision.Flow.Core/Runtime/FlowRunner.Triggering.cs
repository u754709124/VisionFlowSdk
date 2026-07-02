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
        public async Task TriggerAsync(string entryName, FlowToken token, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new ArgumentException("Entry name is required.", "entryName");
            }

            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            CancellationToken runnerToken;
            lock (_gate)
            {
                if (!IsRunning || _runnerCancellation == null)
                {
                    throw new InvalidOperationException("FlowRunner must be started before TriggerAsync is called.");
                }

                runnerToken = _runnerCancellation.Token;
            }

            var entry = FindEntry(entryName);
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
            {
                var linkedToken = linkedCancellation.Token;
                var flowRunId = Guid.NewGuid().ToString("N");
                var variables = new VariablePool();
                await PublishAsync(CreateRuntimeEvent(FlowRuntimeEventType.TokenCreated, token, null, NodeRuntimeState.Waiting, null, null, flowRunId, 0),
                    linkedToken).ConfigureAwait(false);

                await ExecuteGraphAsync(
                    entry.TargetNodeId,
                    token,
                    variables,
                    linkedToken,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    flowRunId).ConfigureAwait(false);
            }
        }

        public async Task DispatchAsync(FlowContinuation continuation, CancellationToken cancellationToken)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException("continuation");
            }

            if (string.IsNullOrWhiteSpace(continuation.SourceNodeId))
            {
                throw new ArgumentException("Continuation source node is required.", "continuation");
            }

            var token = continuation.Token ?? new FlowToken();
            var variables = continuation.Variables ?? new VariablePool();
            CancellationToken runnerToken;
            lock (_gate)
            {
                if (!IsRunning || _runnerCancellation == null)
                {
                    return;
                }

                runnerToken = _runnerCancellation.Token;
            }

            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
            {
                var linkedToken = linkedCancellation.Token;
                var sourceNode = FindNode(continuation.SourceNodeId);
                var outputPort = string.IsNullOrWhiteSpace(continuation.OutputPort) ? FlowPortNames.Next : continuation.OutputPort;
                var result = NodeExecutionResult.Success(outputPort, continuation.Outputs);
                var flowRunId = string.IsNullOrWhiteSpace(continuation.FlowRunId)
                    ? Guid.NewGuid().ToString("N")
                    : continuation.FlowRunId;

                await WriteOutputsAsync(sourceNode, token, result, variables, linkedToken, flowRunId).ConfigureAwait(false);
                await PublishAsync(
                    CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeCompleted,
                        token,
                        sourceNode,
                        NodeRuntimeState.Completed,
                        null,
                        outputPort,
                        flowRunId,
                        0),
                    linkedToken).ConfigureAwait(false);

                if (result.Outputs != null && result.Outputs.ContainsKey(FlowOutputNames.Image))
                {
                    await PublishAsync(
                        CreateRuntimeEvent(
                            FlowRuntimeEventType.ImageProduced,
                            token,
                            sourceNode,
                            NodeRuntimeState.Completed,
                            null,
                            outputPort,
                            flowRunId,
                            0),
                        linkedToken).ConfigureAwait(false);
                }

                var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                path.Add(sourceNode.Id);
                await ExecuteOutgoingEdgesAsync(sourceNode, outputPort, token, variables, linkedToken, path, flowRunId)
                    .ConfigureAwait(false);
            }
        }
    }
}
