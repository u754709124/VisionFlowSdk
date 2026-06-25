using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // 记录节点为 FlowRunner 测试提供确定性的运行探针。
    internal sealed class RecordingNode : IFlowNode
    {
        private readonly NodeDefinition _definition;
        private readonly IList<string> _executionLog;

        public RecordingNode(NodeDefinition definition, IList<string> executionLog)
        {
            _definition = definition;
            _executionLog = executionLog;
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_executionLog)
            {
                _executionLog.Add(_definition.Id);
            }

            var delayMsText = GetSetting("DelayMs");
            int delayMs;
            if (!string.IsNullOrWhiteSpace(delayMsText) &&
                int.TryParse(delayMsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out delayMs) &&
                delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            var mode = GetSetting("Mode");
            if (string.Equals(mode, "Fail", StringComparison.OrdinalIgnoreCase))
            {
                return NodeExecutionResult.Failure("Requested failure.");
            }

            if (string.Equals(mode, "Timeout", StringComparison.OrdinalIgnoreCase))
            {
                var timeoutOutputPort = GetSetting("TimeoutOutputPort");
                return NodeExecutionResult.Timeout("Requested timeout.", timeoutOutputPort);
            }

            if (string.Equals(mode, "ContinueFrame", StringComparison.OrdinalIgnoreCase))
            {
                var continuationOutputs = new Dictionary<string, object>();
                var continuationOutputName = GetSetting("ContinuationOutputName");
                if (!string.IsNullOrWhiteSpace(continuationOutputName))
                {
                    continuationOutputs[continuationOutputName] = GetSetting("ContinuationOutputValue");
                }

                await context.Continuations.DispatchAsync(
                    new FlowContinuation
                    {
                        SourceNodeId = _definition.Id,
                        OutputPort = "Frame",
                        Token = context.Token,
                        Variables = context.Variables,
                        Outputs = continuationOutputs,
                        FlowRunId = context.FlowRunId
                    },
                    cancellationToken).ConfigureAwait(false);
                return NodeExecutionResult.Success("Completed");
            }

            var requiredVariable = GetSetting("RequiredVariable");
            if (!string.IsNullOrWhiteSpace(requiredVariable))
            {
                object value;
                if (!context.Variables.TryGet(requiredVariable, out value))
                {
                    return NodeExecutionResult.Failure("Required variable was missing: " + requiredVariable);
                }
            }

            var outputs = new Dictionary<string, object>();
            var outputName = GetSetting("OutputName");
            if (!string.IsNullOrWhiteSpace(outputName))
            {
                outputs[outputName] = GetSetting("OutputValue");
            }

            return NodeExecutionResult.Success("Next", outputs);
        }

        private string GetSetting(string name)
        {
            object value;
            if (_definition.Settings != null && _definition.Settings.TryGetValue(name, out value))
            {
                return Convert.ToString(value);
            }

            return null;
        }
    }
}
