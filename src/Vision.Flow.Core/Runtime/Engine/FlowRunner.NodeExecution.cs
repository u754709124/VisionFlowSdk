using System;
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
            CancellationToken cancellationToken,
            string flowRunId)
        {
            await PublishAsync(CreateRuntimeEvent(FlowRuntimeEventType.NodeStarted, token, node, NodeRuntimeState.Running, null, null, flowRunId, 0),
                cancellationToken).ConfigureAwait(false);

            NodeExecutionResult result;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var flowNode = GetOrCreateNode(node);
                var context = new FlowExecutionContext(_definition, node, token, variables, _eventSink, _devices, _cameraFrames, this, flowRunId);
                result = await flowNode.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                if (result == null)
                {
                    result = NodeExecutionResult.Failure("Node returned a null execution result.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = NodeExecutionResult.Failure(ex.Message);
            }

            if (result.IsTimeout)
            {
                stopwatch.Stop();
                await PublishAsync(
                    CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeTimeout,
                        token,
                        node,
                        NodeRuntimeState.Timeout,
                        result.ErrorMessage,
                        string.IsNullOrWhiteSpace(result.OutputPort) ? FlowPortNames.Error : result.OutputPort,
                        flowRunId,
                        stopwatch.ElapsedMilliseconds),
                    cancellationToken).ConfigureAwait(false);
                return result;
            }

            if (!result.IsSuccess)
            {
                stopwatch.Stop();
                await PublishAsync(
                    CreateRuntimeEvent(
                        FlowRuntimeEventType.NodeFailed,
                        token,
                        node,
                        NodeRuntimeState.Failed,
                        result.ErrorMessage,
                        string.IsNullOrWhiteSpace(result.OutputPort) ? FlowPortNames.Error : result.OutputPort,
                        flowRunId,
                        stopwatch.ElapsedMilliseconds),
                    cancellationToken).ConfigureAwait(false);
                return result;
            }

            stopwatch.Stop();
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
            return result;
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
    }
}
