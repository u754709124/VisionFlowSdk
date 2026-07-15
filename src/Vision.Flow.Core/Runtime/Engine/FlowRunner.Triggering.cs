using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner
    {
        public Task<FlowRunResult> TriggerAsync(
            FlowTriggerRequest request,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (string.IsNullOrWhiteSpace(request.EntryName))
            {
                throw new ArgumentException("Entry name is required.", "request");
            }

            var entry = FindEntry(request.EntryName);
            var token = request.Token ?? new FlowToken();
            return ExecuteEntryRunAsync(
                entry,
                request.Source,
                token,
                request.Inputs,
                null,
                null,
                cancellationToken);
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

            if (!string.IsNullOrWhiteSpace(continuation.EntryName))
            {
                var entry = FindEntry(continuation.EntryName);
                await ExecuteEntryRunAsync(
                    entry,
                    FlowTriggerSource.NodeEvent,
                    continuation.Token ?? new FlowToken(),
                    continuation.TriggerInputs,
                    continuation,
                    continuation.FlowRunId,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            await DispatchExistingRunContinuationAsync(continuation, cancellationToken).ConfigureAwait(false);
        }

        private async Task<FlowRunResult> ExecuteEntryRunAsync(
            FlowEntryDefinition entry,
            FlowTriggerSource source,
            FlowToken token,
            IDictionary<string, object> providedInputs,
            FlowContinuation nodeEventContinuation,
            string requestedFlowRunId,
            CancellationToken cancellationToken)
        {
            CancellationToken runnerToken;
            lock (_gate)
            {
                if (!IsRunning || _runnerCancellation == null)
                {
                    throw new InvalidOperationException("FlowRunner must be started before TriggerAsync is called.");
                }

                runnerToken = _runnerCancellation.Token;
            }

            var result = new FlowRunResult
            {
                FlowRunId = string.IsNullOrWhiteSpace(requestedFlowRunId) ? Guid.NewGuid().ToString("N") : requestedFlowRunId,
                EntryName = entry.EntryName,
                Source = source,
                Token = token,
                StartedAtUtc = DateTime.UtcNow
            };
            IDictionary<string, object> triggerInputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string rejectionReason = null;
            if (!IsTriggerSourceAllowed(entry.TriggerKind, source))
            {
                rejectionReason = "Trigger source " + source + " does not match entry kind " + entry.TriggerKind + ".";
            }
            else if (entry.TriggerKind == FlowTriggerKind.NodeEvent && nodeEventContinuation == null)
            {
                rejectionReason = "NodeEvent entries can only be triggered by their listener continuation.";
            }
            else if (!TryPrepareTriggerInputs(entry, providedInputs, out triggerInputs, out rejectionReason))
            {
                triggerInputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            await PublishTokenCreatedAsync(result, triggerInputs).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(rejectionReason))
            {
                return await CompleteFlowRunAsync(result, FlowRunStatus.Rejected, rejectionReason, null).ConfigureAwait(false);
            }

            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
            {
                EntryExecutionLease lease = null;
                IVariablePool variables = null;
                try
                {
                    lease = await GetEntryGate(entry).TryEnterAsync(linkedCancellation.Token).ConfigureAwait(false);
                    if (lease == null)
                    {
                        return await CompleteFlowRunAsync(
                            result,
                            FlowRunStatus.Rejected,
                            "Trigger queue is full for entry: " + entry.EntryName,
                            triggerInputs).ConfigureAwait(false);
                    }

                    await PublishFlowRunEventAsync(
                        FlowRuntimeEventType.FlowRunStarted,
                        result,
                        NodeRuntimeState.Running,
                        null,
                        triggerInputs).ConfigureAwait(false);

                    variables = nodeEventContinuation == null || nodeEventContinuation.Variables == null
                        ? new VariablePool()
                        : nodeEventContinuation.Variables;
                    if (nodeEventContinuation == null)
                    {
                        await ExecuteGraphAsync(
                            entry.TargetNodeId,
                            token,
                            variables,
                            triggerInputs,
                            linkedCancellation.Token,
                            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                            result.FlowRunId).ConfigureAwait(false);
                    }
                    else
                    {
                        await ExecuteNodeEventContinuationAsync(
                            entry,
                            nodeEventContinuation,
                            token,
                            variables,
                            triggerInputs,
                            linkedCancellation.Token,
                            result.FlowRunId).ConfigureAwait(false);
                    }

                    return await CompleteFlowRunAsync(result, FlowRunStatus.Succeeded, null, triggerInputs, variables).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                {
                    return await CompleteFlowRunAsync(result, FlowRunStatus.Cancelled, ex.Message, triggerInputs, variables).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return await CompleteFlowRunAsync(result, FlowRunStatus.Failed, ex.Message, triggerInputs, variables).ConfigureAwait(false);
                }
                finally
                {
                    if (lease != null)
                    {
                        lease.Dispose();
                    }
                }
            }
        }

        private async Task ExecuteNodeEventContinuationAsync(
            FlowEntryDefinition entry,
            FlowContinuation continuation,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            if (string.IsNullOrWhiteSpace(entry.SourceNodeId) ||
                !string.Equals(entry.SourceNodeId, continuation.SourceNodeId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("NodeEvent continuation source does not match entry SourceNodeId.");
            }

            var sourceNode = FindNode(entry.SourceNodeId);
            EnsureReadyQueueScopeIsExecutable(sourceNode.Id);
            var outputPort = string.IsNullOrWhiteSpace(continuation.OutputPort) ? FlowPortNames.Next : continuation.OutputPort;
            var nodeResult = NodeExecutionResult.Success(outputPort, continuation.Outputs);
            await WriteOutputsAsync(sourceNode, token, nodeResult, variables, cancellationToken, flowRunId).ConfigureAwait(false);
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
                cancellationToken).ConfigureAwait(false);

            if (nodeResult.Outputs != null && nodeResult.Outputs.ContainsKey(FlowOutputNames.Image))
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
                    cancellationToken).ConfigureAwait(false);
            }

            var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sourceNode.Id };
            await ExecuteOutgoingEdgesAsync(
                sourceNode,
                outputPort,
                token,
                variables,
                triggerInputs,
                cancellationToken,
                path,
                flowRunId).ConfigureAwait(false);
        }

        private async Task DispatchExistingRunContinuationAsync(
            FlowContinuation continuation,
            CancellationToken cancellationToken)
        {
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
                var token = continuation.Token ?? new FlowToken();
                var variables = continuation.Variables ?? new VariablePool();
                var triggerInputs = continuation.TriggerInputs ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var sourceNode = FindNode(continuation.SourceNodeId);
                EnsureReadyQueueScopeIsExecutable(sourceNode.Id);
                var outputPort = string.IsNullOrWhiteSpace(continuation.OutputPort) ? FlowPortNames.Next : continuation.OutputPort;
                var nodeResult = NodeExecutionResult.Success(outputPort, continuation.Outputs);
                var flowRunId = string.IsNullOrWhiteSpace(continuation.FlowRunId)
                    ? Guid.NewGuid().ToString("N")
                    : continuation.FlowRunId;

                await WriteOutputsAsync(sourceNode, token, nodeResult, variables, linkedCancellation.Token, flowRunId).ConfigureAwait(false);
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
                    linkedCancellation.Token).ConfigureAwait(false);

                if (nodeResult.Outputs != null && nodeResult.Outputs.ContainsKey(FlowOutputNames.Image))
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
                        linkedCancellation.Token).ConfigureAwait(false);
                }

                var path = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { sourceNode.Id };
                await ExecuteOutgoingEdgesAsync(
                    sourceNode,
                    outputPort,
                    token,
                    variables,
                    triggerInputs,
                    linkedCancellation.Token,
                    path,
                    flowRunId).ConfigureAwait(false);
            }
        }

        private async Task<FlowRunResult> CompleteFlowRunAsync(
            FlowRunResult result,
            FlowRunStatus status,
            string errorMessage,
            IDictionary<string, object> triggerInputs,
            IVariablePool variables = null)
        {
            result.Status = status;
            result.ErrorMessage = errorMessage;
            result.CompletedAtUtc = DateTime.UtcNow;
            result.Variables = status == FlowRunStatus.Rejected || variables == null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : variables.Snapshot();
            var eventType = status == FlowRunStatus.Succeeded
                ? FlowRuntimeEventType.FlowRunCompleted
                : status == FlowRunStatus.Cancelled
                    ? FlowRuntimeEventType.FlowRunCancelled
                    : status == FlowRunStatus.Rejected
                        ? FlowRuntimeEventType.FlowRunRejected
                        : FlowRuntimeEventType.FlowRunFailed;
            var state = status == FlowRunStatus.Succeeded
                ? NodeRuntimeState.Completed
                : status == FlowRunStatus.Failed
                    ? NodeRuntimeState.Failed
                    : NodeRuntimeState.Stopped;
            await PublishFlowRunEventAsync(eventType, result, state, errorMessage, triggerInputs).ConfigureAwait(false);
            return result;
        }

        private Task PublishFlowRunEventAsync(
            FlowRuntimeEventType eventType,
            FlowRunResult result,
            NodeRuntimeState state,
            string message,
            IDictionary<string, object> triggerInputs)
        {
            var elapsedMs = result.CompletedAtUtc == default(DateTime)
                ? 0
                : (long)Math.Max(0, (result.CompletedAtUtc - result.StartedAtUtc).TotalMilliseconds);
            var runtimeEvent = CreateRuntimeEvent(
                eventType,
                result.Token,
                null,
                state,
                message,
                null,
                result.FlowRunId,
                elapsedMs);
            runtimeEvent.Data[FlowRuntimeDataKeys.EntryName] = result.EntryName;
            runtimeEvent.Data[FlowRuntimeDataKeys.TriggerSource] = result.Source.ToString();
            runtimeEvent.Data[FlowRuntimeDataKeys.TriggerInputs] = triggerInputs == null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(triggerInputs, StringComparer.OrdinalIgnoreCase);
            runtimeEvent.Data[FlowRuntimeDataKeys.FlowRunStatus] = eventType == FlowRuntimeEventType.FlowRunStarted
                ? "Running"
                : result.Status.ToString();
            return PublishAsync(runtimeEvent, CancellationToken.None);
        }

        private Task PublishTokenCreatedAsync(
            FlowRunResult result,
            IDictionary<string, object> triggerInputs)
        {
            var runtimeEvent = CreateRuntimeEvent(
                FlowRuntimeEventType.TokenCreated,
                result.Token,
                null,
                NodeRuntimeState.Waiting,
                null,
                null,
                result.FlowRunId,
                0);
            runtimeEvent.Data[FlowRuntimeDataKeys.EntryName] = result.EntryName;
            runtimeEvent.Data[FlowRuntimeDataKeys.TriggerSource] = result.Source.ToString();
            runtimeEvent.Data[FlowRuntimeDataKeys.TriggerInputs] = triggerInputs == null
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(triggerInputs, StringComparer.OrdinalIgnoreCase);
            return PublishAsync(runtimeEvent, CancellationToken.None);
        }

        private static bool IsTriggerSourceAllowed(FlowTriggerKind kind, FlowTriggerSource source)
        {
            return (kind == FlowTriggerKind.Manual && source == FlowTriggerSource.Manual) ||
                (kind == FlowTriggerKind.External && source == FlowTriggerSource.External) ||
                (kind == FlowTriggerKind.NodeEvent && source == FlowTriggerSource.NodeEvent);
        }
    }
}
