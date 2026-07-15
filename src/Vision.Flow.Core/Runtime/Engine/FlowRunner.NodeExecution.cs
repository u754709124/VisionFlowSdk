using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private async Task<NodeExecutionResult> ExecuteNodeAsync(
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            var policy = node.ExecutionPolicy ?? new NodeExecutionPolicy();
            var retryPolicy = policy.RetryPolicy ?? new RetryPolicy();
            var maxRetries = retryPolicy.Enabled ? Math.Max(0, retryPolicy.MaxRetries) : 0;
            var retryIntervalMs = Math.Max(0, retryPolicy.RetryIntervalMs);
            var timeoutMs = policy.TimeoutMs > 0
                ? policy.TimeoutMs
                : Math.Max(0, _options.DefaultNodeTimeoutMs);
            var stopwatch = Stopwatch.StartNew();
            var attempt = 0;
            NodeExecutionResult result = null;
            NodeFailureKind lastFailureKind = NodeFailureKind.None;

            try
            {
                while (attempt <= maxRetries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    attempt++;

                    var startedEvent = CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeStarted,
                        token,
                        node,
                        NodeRuntimeState.Running,
                        null,
                        null,
                        flowRunId,
                        0);
                    startedEvent.Data[FlowRuntimeDataKeys.Attempt] = attempt;
                    startedEvent.Data[FlowRuntimeDataKeys.FailureKind] = NodeFailureKind.None.ToString();
                    startedEvent.Data[FlowRuntimeDataKeys.FailureStrategy] = policy.FailureStrategy.ToString();
                    await PublishAsync(startedEvent, cancellationToken).ConfigureAwait(false);

                    result = await ExecuteNodeAttemptAsync(
                        node,
                        token,
                        variables,
                        triggerInputs,
                        cancellationToken,
                        flowRunId,
                        timeoutMs).ConfigureAwait(false);

                    if (result.IsSuccess)
                    {
                        break;
                    }

                    lastFailureKind = NormalizeFailureKind(result);
                    if (!CanRetry(result) || attempt > maxRetries)
                    {
                        break;
                    }

                    var retryingEvent = CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeRetrying,
                        token,
                        node,
                        NodeRuntimeState.Waiting,
                        result.ErrorMessage,
                        result.OutputPort,
                        flowRunId,
                        stopwatch.ElapsedMilliseconds);
                    retryingEvent.Data[FlowRuntimeDataKeys.Attempt] = attempt + 1;
                    retryingEvent.Data[FlowRuntimeDataKeys.FailureKind] = lastFailureKind.ToString();
                    retryingEvent.Data[FlowRuntimeDataKeys.FailureStrategy] = policy.FailureStrategy.ToString();
                    await PublishAsync(retryingEvent, cancellationToken).ConfigureAwait(false);

                    if (retryIntervalMs > 0)
                    {
                        await Task.Delay(retryIntervalMs, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                var cancelledEvent = CreateRuntimeEvent(
                    FlowRuntimeEventType.NodeCancelled,
                    token,
                    node,
                    NodeRuntimeState.Stopped,
                    "Node execution was cancelled.",
                    null,
                    flowRunId,
                    stopwatch.ElapsedMilliseconds);
                cancelledEvent.Data[FlowRuntimeDataKeys.Attempt] = Math.Max(1, attempt);
                cancelledEvent.Data[FlowRuntimeDataKeys.FailureKind] = NodeFailureKind.Cancelled.ToString();
                cancelledEvent.Data[FlowRuntimeDataKeys.FailureStrategy] = policy.FailureStrategy.ToString();
                await PublishAsync(cancelledEvent, CancellationToken.None).ConfigureAwait(false);
                throw;
            }

            stopwatch.Stop();
            if (result == null)
            {
                result = NodeExecutionResult.Failure("Node returned a null execution result.");
            }

            if (result.IsTimeout)
            {
                await PublishFinalFailureAsync(
                    FlowRuntimeEventType.NodeTimeout,
                    NodeRuntimeState.Timeout,
                    node,
                    token,
                    result,
                    policy.FailureStrategy,
                    attempt,
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
                return await ApplyFailureStrategyAsync(
                    node,
                    token,
                    variables,
                    result,
                    policy,
                    attempt,
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
            }

            if (!result.IsSuccess)
            {
                await PublishFinalFailureAsync(
                    FlowRuntimeEventType.NodeFailed,
                    NodeRuntimeState.Failed,
                    node,
                    token,
                    result,
                    policy.FailureStrategy,
                    attempt,
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
                return await ApplyFailureStrategyAsync(
                    node,
                    token,
                    variables,
                    result,
                    policy,
                    attempt,
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
            }

            if (attempt > 1)
            {
                await PublishRecoveredAsync(
                    node,
                    token,
                    result,
                    attempt,
                    lastFailureKind,
                    "Retry",
                    stopwatch.ElapsedMilliseconds,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
            }

            await WriteOutputsAsync(node, token, result, variables, cancellationToken, flowRunId).ConfigureAwait(false);
            await PublishAsync(
                CreateRuntimeEvent(
                    FlowRuntimeEventType.NodeCompleted,
                    token,
                    node,
                    NodeRuntimeState.Completed,
                    null,
                    string.IsNullOrWhiteSpace(result.OutputPort) ? FlowPortNames.Next : result.OutputPort,
                    flowRunId,
                    stopwatch.ElapsedMilliseconds),
                cancellationToken).ConfigureAwait(false);
            await PublishImageProducedAsync(node, token, result, cancellationToken, flowRunId).ConfigureAwait(false);
            return result;
        }

        private async Task<NodeExecutionResult> ExecuteNodeAttemptAsync(
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs,
            CancellationToken cancellationToken,
            string flowRunId,
            int timeoutMs)
        {
            try
            {
                var flowNode = GetOrCreateNode(node);
                var continuations = new BoundFlowContinuationDispatcher(
                    this,
                    null,
                    flowRunId,
                    token,
                    variables,
                    triggerInputs);
                var context = new FlowExecutionContext(
                    _definition,
                    node,
                    token,
                    variables,
                    _eventSink,
                    _devices,
                    continuations,
                    flowRunId,
                    triggerInputs);

                using (var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    var executionTask = flowNode.ExecuteAsync(context, attemptCancellation.Token);
                    if (executionTask == null)
                    {
                        return NodeExecutionResult.Failure("Node returned a null execution task.");
                    }

                    if (timeoutMs <= 0)
                    {
                        return NormalizeResult(await executionTask.ConfigureAwait(false));
                    }

                    var timeoutTask = Task.Delay(timeoutMs, cancellationToken);
                    var completedTask = await Task.WhenAny(executionTask, timeoutTask).ConfigureAwait(false);
                    if (completedTask == executionTask)
                    {
                        return NormalizeResult(await executionTask.ConfigureAwait(false));
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    attemptCancellation.Cancel();
                    ObserveLateFailure(executionTask);
                    return NodeExecutionResult.Timeout(
                        "Node execution timed out after " + timeoutMs + " ms.",
                        FlowPortNames.Error);
                }
            }
            catch (SettingBindingException ex)
            {
                return NodeExecutionResult.Failure(ex.Message, FlowPortNames.Error, NodeFailureKind.Binding);
            }
            catch (NodeConfigurationException ex)
            {
                return NodeExecutionResult.Failure(ex.Message, FlowPortNames.Error, NodeFailureKind.Configuration);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return NodeExecutionResult.Failure(ex.Message, FlowPortNames.Error, NodeFailureKind.Execution);
            }
        }

        private async Task<NodeExecutionResult> ApplyFailureStrategyAsync(
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            NodeExecutionResult failure,
            NodeExecutionPolicy policy,
            int attempt,
            long elapsedMs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            var failureKind = NormalizeFailureKind(failure);
            var strategy = Enum.IsDefined(typeof(FailureStrategy), policy.FailureStrategy)
                ? policy.FailureStrategy
                : FailureStrategy.StopFlow;

            if (strategy == FailureStrategy.ErrorBranch)
            {
                var outputPort = string.IsNullOrWhiteSpace(failure.OutputPort)
                    ? FlowPortNames.Error
                    : failure.OutputPort;
                if (_plan.GetOutgoingEdges(node.Id, outputPort).Count == 0)
                {
                    throw CreateExecutionFailedException(node, failure, failureKind, "ErrorBranch has no connected output edge.");
                }

                await PublishRecoveredAsync(
                    node,
                    token,
                    failure,
                    attempt,
                    failureKind,
                    strategy.ToString(),
                    elapsedMs,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
                await PublishAsync(
                    CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeCompleted,
                        token,
                        node,
                        NodeRuntimeState.Completed,
                        null,
                        outputPort,
                        flowRunId,
                        elapsedMs),
                    cancellationToken).ConfigureAwait(false);
                return failure;
            }

            if (strategy == FailureStrategy.DefaultOutputs)
            {
                var recoveredResult = NodeExecutionResult.Success(
                    FlowPortNames.Next,
                    policy.DefaultOutputs ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
                await PublishRecoveredAsync(
                    node,
                    token,
                    recoveredResult,
                    attempt,
                    failureKind,
                    strategy.ToString(),
                    elapsedMs,
                    cancellationToken,
                    flowRunId).ConfigureAwait(false);
                await WriteOutputsAsync(node, token, recoveredResult, variables, cancellationToken, flowRunId).ConfigureAwait(false);
                await PublishAsync(
                    CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeCompleted,
                        token,
                        node,
                        NodeRuntimeState.Completed,
                        null,
                        recoveredResult.OutputPort,
                        flowRunId,
                        elapsedMs),
                    cancellationToken).ConfigureAwait(false);
                await PublishImageProducedAsync(node, token, recoveredResult, cancellationToken, flowRunId).ConfigureAwait(false);
                return recoveredResult;
            }

            throw CreateExecutionFailedException(node, failure, failureKind, null);
        }

        private async Task PublishFinalFailureAsync(
            FlowRuntimeEventType eventType,
            NodeRuntimeState state,
            NodeDefinition node,
            FlowToken token,
            NodeExecutionResult result,
            FailureStrategy failureStrategy,
            int attempt,
            long elapsedMs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            var runtimeEvent = CreateRuntimeEvent(
                eventType,
                token,
                node,
                state,
                result.ErrorMessage,
                string.IsNullOrWhiteSpace(result.OutputPort) ? FlowPortNames.Error : result.OutputPort,
                flowRunId,
                elapsedMs);
            runtimeEvent.Data[FlowRuntimeDataKeys.Attempt] = Math.Max(1, attempt);
            runtimeEvent.Data[FlowRuntimeDataKeys.FailureKind] = NormalizeFailureKind(result).ToString();
            runtimeEvent.Data[FlowRuntimeDataKeys.FailureStrategy] = failureStrategy.ToString();
            await PublishAsync(runtimeEvent, cancellationToken).ConfigureAwait(false);
        }

        private async Task PublishRecoveredAsync(
            NodeDefinition node,
            FlowToken token,
            NodeExecutionResult result,
            int attempt,
            NodeFailureKind failureKind,
            string recoveryStrategy,
            long elapsedMs,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            var runtimeEvent = CreateRuntimeEvent(
                FlowRuntimeEventType.NodeRecovered,
                token,
                node,
                NodeRuntimeState.Completed,
                null,
                result.OutputPort,
                flowRunId,
                elapsedMs);
            runtimeEvent.Data[FlowRuntimeDataKeys.Attempt] = Math.Max(1, attempt);
            runtimeEvent.Data[FlowRuntimeDataKeys.FailureKind] = failureKind.ToString();
            runtimeEvent.Data[FlowRuntimeDataKeys.FailureStrategy] = recoveryStrategy;
            await PublishAsync(runtimeEvent, cancellationToken).ConfigureAwait(false);
        }

        private static NodeExecutionResult NormalizeResult(NodeExecutionResult result)
        {
            return result ?? NodeExecutionResult.Failure("Node returned a null execution result.");
        }

        private static NodeFailureKind NormalizeFailureKind(NodeExecutionResult result)
        {
            if (result == null)
            {
                return NodeFailureKind.Execution;
            }

            if (result.IsTimeout)
            {
                result.FailureKind = NodeFailureKind.Timeout;
                return NodeFailureKind.Timeout;
            }

            if (!result.IsSuccess && result.FailureKind == NodeFailureKind.None)
            {
                result.FailureKind = NodeFailureKind.Execution;
            }

            return result.FailureKind;
        }

        private static bool CanRetry(NodeExecutionResult result)
        {
            var failureKind = NormalizeFailureKind(result);
            return !result.IsSuccess &&
                (failureKind == NodeFailureKind.Execution || failureKind == NodeFailureKind.Timeout);
        }

        private static NodeExecutionFailedException CreateExecutionFailedException(
            NodeDefinition node,
            NodeExecutionResult failure,
            NodeFailureKind failureKind,
            string suffix)
        {
            var message = "Node '" + node.Id + "' failed";
            if (!string.IsNullOrWhiteSpace(failure.ErrorMessage))
            {
                message += ": " + failure.ErrorMessage;
            }

            if (!string.IsNullOrWhiteSpace(suffix))
            {
                message += " " + suffix;
            }

            return new NodeExecutionFailedException(message, failureKind);
        }

        private static void ObserveLateFailure(Task<NodeExecutionResult> executionTask)
        {
            executionTask.ContinueWith(
                task =>
                {
                    var ignored = task.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private async Task WriteOutputsAsync(
            NodeDefinition node,
            FlowToken token,
            NodeExecutionResult result,
            IVariablePool variables,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            if (result.Outputs == null)
            {
                return;
            }

            foreach (var output in result.Outputs)
            {
                var variableName = node.Id + "." + output.Key;
                variables.Set(variableName, output.Value);

                var runtimeEvent = CreateRuntimeEvent(
                    FlowRuntimeEventType.OutputProduced,
                    token,
                    node,
                    NodeRuntimeState.Completed,
                    null,
                    result.OutputPort,
                    flowRunId,
                    0);
                runtimeEvent.Data[FlowRuntimeDataKeys.VariableName] = variableName;
                runtimeEvent.Data[FlowRuntimeDataKeys.Value] = output.Value;
                await PublishAsync(runtimeEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task PublishImageProducedAsync(
            NodeDefinition node,
            FlowToken token,
            NodeExecutionResult result,
            CancellationToken cancellationToken,
            string flowRunId)
        {
            if (result == null || result.Outputs == null || !result.Outputs.ContainsKey(FlowOutputNames.Image))
            {
                return;
            }

            await PublishAsync(
                CreateRuntimeEvent(
                    FlowRuntimeEventType.ImageProduced,
                    token,
                    node,
                    NodeRuntimeState.Completed,
                    null,
                    result.OutputPort,
                    flowRunId,
                    0),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
