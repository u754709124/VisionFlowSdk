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
        /// <summary>
        /// 从命名入口触发一次运行，并返回唯一终态及最终变量快照。
        /// </summary>
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

        /// <summary>
        /// 调度节点续流；沿用 Active FlowRunId 时归入原运行，否则建立独立的监听续流生命周期。
        /// </summary>
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
            EnsureTokenId(token);
            var result = new FlowRunResult
            {
                FlowRunId = string.IsNullOrWhiteSpace(requestedFlowRunId) ? Guid.NewGuid().ToString("N") : requestedFlowRunId,
                EntryName = entry.EntryName,
                Source = source,
                Token = token,
                StartedAtUtc = DateTime.UtcNow
            };
            CancellationToken runnerToken = CancellationToken.None;
            ActiveFlowRun activeRun = null;
            var rejectedByStopping = false;
            lock (_gate)
            {
                if (!IsRunning || _runnerCancellation == null)
                {
                    if (source != FlowTriggerSource.NodeEvent)
                    {
                        throw new InvalidOperationException("FlowRunner must be started before TriggerAsync is called.");
                    }

                    rejectedByStopping = true;
                    if (_isStopping)
                    {
                        // StopCore 会先等待监听器退出再截取 Active 快照；把竞态窗口内的拒绝续流也纳入排空。
                        activeRun = RegisterActiveFlowRun(result.FlowRunId);
                    }
                }
                else
                {
                    runnerToken = _runnerCancellation.Token;
                    activeRun = RegisterActiveFlowRun(result.FlowRunId);
                }
            }

            IDictionary<string, object> triggerInputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string rejectionReason = null;
            if (rejectedByStopping)
            {
                rejectionReason = "FlowRunner is stopping and no longer accepts listener continuations.";
            }
            else if (!IsTriggerSourceAllowed(entry.TriggerKind, source))
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

            EntryExecutionLease lease = null;
            IVariablePool variables = null;
            var status = FlowRunStatus.Succeeded;
            string errorMessage = null;
            try
            {
                await PublishTokenCreatedAsync(result, triggerInputs).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(rejectionReason))
                {
                    status = FlowRunStatus.Rejected;
                    errorMessage = rejectionReason;
                }
                else
                {
                    using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
                    {
                        lease = await GetEntryGate(entry).TryEnterAsync(linkedCancellation.Token).ConfigureAwait(false);
                        if (lease == null)
                        {
                            status = FlowRunStatus.Rejected;
                            errorMessage = "Trigger queue is full for entry: " + entry.EntryName;
                        }
                        else
                        {
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
                        }
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                status = FlowRunStatus.Cancelled;
                errorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                status = FlowRunStatus.Failed;
                errorMessage = ex.Message;
            }
            finally
            {
                if (lease != null)
                {
                    lease.Dispose();
                }
            }

            return await CompleteFlowRunAsync(
                activeRun,
                result,
                status,
                errorMessage,
                triggerInputs,
                variables).ConfigureAwait(false);
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
            var token = continuation.Token ?? new FlowToken();
            EnsureTokenId(token);
            var variables = continuation.Variables ?? new VariablePool();
            var triggerInputs = continuation.TriggerInputs ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var result = new FlowRunResult
            {
                FlowRunId = string.IsNullOrWhiteSpace(continuation.FlowRunId)
                    ? Guid.NewGuid().ToString("N")
                    : continuation.FlowRunId,
                EntryName = string.Empty,
                Source = FlowTriggerSource.NodeEvent,
                Token = token,
                StartedAtUtc = DateTime.UtcNow
            };
            CancellationToken runnerToken = CancellationToken.None;
            ActiveFlowRun activeRun = null;
            var rejectedByStopping = false;
            var belongsToActiveRun = false;
            lock (_gate)
            {
                if (!IsRunning || _runnerCancellation == null)
                {
                    rejectedByStopping = true;
                    if (_isStopping)
                    {
                        activeRun = RegisterActiveFlowRun(result.FlowRunId);
                    }
                }
                else
                {
                    runnerToken = _runnerCancellation.Token;
                    ActiveFlowRun existingRun;
                    belongsToActiveRun =
                        !string.IsNullOrWhiteSpace(continuation.FlowRunId) &&
                        _activeFlowRuns.TryGetValue(result.FlowRunId, out existingRun);
                    if (!belongsToActiveRun)
                    {
                        activeRun = RegisterActiveFlowRun(result.FlowRunId);
                    }
                }
            }

            if (belongsToActiveRun)
            {
                using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
                {
                    await ExecuteContinuationGraphAsync(
                        continuation,
                        token,
                        variables,
                        triggerInputs,
                        linkedCancellation.Token,
                        result.FlowRunId).ConfigureAwait(false);
                }

                return;
            }

            Exception dispatchError = null;
            var status = rejectedByStopping ? FlowRunStatus.Rejected : FlowRunStatus.Succeeded;
            try
            {
                await PublishTokenCreatedAsync(result, triggerInputs).ConfigureAwait(false);
                if (!rejectedByStopping)
                {
                    await PublishFlowRunEventAsync(
                        FlowRuntimeEventType.FlowRunStarted,
                        result,
                        NodeRuntimeState.Running,
                        null,
                        triggerInputs).ConfigureAwait(false);

                    using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
                    {
                        await ExecuteContinuationGraphAsync(
                            continuation,
                            token,
                            variables,
                            triggerInputs,
                            linkedCancellation.Token,
                            result.FlowRunId).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                status = FlowRunStatus.Cancelled;
                dispatchError = ex;
            }
            catch (Exception ex)
            {
                status = FlowRunStatus.Failed;
                dispatchError = ex;
            }

            await CompleteFlowRunAsync(
                activeRun,
                result,
                status,
                rejectedByStopping
                    ? "FlowRunner is stopping and no longer accepts listener continuations."
                    : dispatchError == null ? null : dispatchError.Message,
                triggerInputs,
                variables).ConfigureAwait(false);
            if (dispatchError != null)
            {
                throw dispatchError;
            }
        }

        /// <summary>
        /// 修复外部调用方显式传入的空令牌标识，确保所有入口和续流事件都可稳定关联。
        /// </summary>
        private static void EnsureTokenId(FlowToken token)
        {
            if (token == null)
                throw new ArgumentNullException("token");
            if (string.IsNullOrWhiteSpace(token.TokenId))
                token.TokenId = Guid.NewGuid().ToString("N");
        }

        private async Task ExecuteContinuationGraphAsync(
            FlowContinuation continuation,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            var sourceNode = FindNode(continuation.SourceNodeId);
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

        private async Task<FlowRunResult> CompleteFlowRunAsync(
            ActiveFlowRun activeRun,
            FlowRunResult result,
            FlowRunStatus status,
            string errorMessage,
            IDictionary<string, object> triggerInputs,
            IVariablePool variables = null)
        {
            if (activeRun != null && !activeRun.TryClaimTerminal())
            {
                return result;
            }

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
            try
            {
                await PublishFlowRunEventAsync(eventType, result, state, errorMessage, triggerInputs).ConfigureAwait(false);
                return result;
            }
            finally
            {
                CompleteActiveFlowRun(activeRun);
            }
        }

        private ActiveFlowRun RegisterActiveFlowRun(string flowRunId)
        {
            ActiveFlowRun existing;
            if (_activeFlowRuns.TryGetValue(flowRunId, out existing))
            {
                throw new InvalidOperationException("An active FlowRun already uses FlowRunId: " + flowRunId);
            }

            var activeRun = new ActiveFlowRun(flowRunId);
            _activeFlowRuns.Add(flowRunId, activeRun);
            return activeRun;
        }

        private void CompleteActiveFlowRun(ActiveFlowRun activeRun)
        {
            if (activeRun == null)
            {
                return;
            }

            lock (_gate)
            {
                ActiveFlowRun registered;
                if (_activeFlowRuns.TryGetValue(activeRun.FlowRunId, out registered) &&
                    ReferenceEquals(registered, activeRun))
                {
                    _activeFlowRuns.Remove(activeRun.FlowRunId);
                }
            }

            // 完成信号必须晚于终态事件发布；宿主据此安全释放 FlowRun 级资源。
            activeRun.MarkCompleted();
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
