using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Engine
{
    /// <summary>
    /// 为节点续流补齐入口、运行 ID、Token、变量池和触发输入，确保同一运行上下文不会丢失。
    /// </summary>
    internal sealed class BoundFlowContinuationDispatcher : IFlowContinuationDispatcher
    {
        private readonly IFlowContinuationDispatcher _inner;
        private readonly string _sourceNodeId;
        private readonly string _entryName;
        private readonly string _flowRunId;
        private readonly FlowToken _token;
        private readonly IVariablePool _variables;
        private readonly IDictionary<string, object> _triggerInputs;

        public BoundFlowContinuationDispatcher(
            IFlowContinuationDispatcher inner,
            string sourceNodeId,
            string entryName,
            string flowRunId,
            FlowToken token,
            IVariablePool variables,
            IDictionary<string, object> triggerInputs)
        {
            if (inner == null)
            {
                throw new ArgumentNullException("inner");
            }

            _inner = inner;
            _sourceNodeId = sourceNodeId;
            _entryName = entryName;
            _flowRunId = flowRunId;
            _token = token;
            _variables = variables;
            _triggerInputs = triggerInputs;
        }

        public Task DispatchAsync(FlowContinuation continuation, CancellationToken cancellationToken)
        {
            if (continuation == null)
            {
                throw new ArgumentNullException("continuation");
            }

            if (string.IsNullOrWhiteSpace(continuation.SourceNodeId))
            {
                continuation.SourceNodeId = _sourceNodeId;
            }
            else if (!string.IsNullOrWhiteSpace(_sourceNodeId) &&
                !string.Equals(continuation.SourceNodeId, _sourceNodeId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Continuation source node does not match its execution context: " + continuation.SourceNodeId);
            }

            if (string.IsNullOrWhiteSpace(continuation.EntryName))
            {
                continuation.EntryName = _entryName;
            }
            else if (!string.IsNullOrWhiteSpace(_entryName) &&
                !string.Equals(continuation.EntryName, _entryName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Continuation entry does not match its listener context: " + continuation.EntryName);
            }

            if (string.IsNullOrWhiteSpace(continuation.FlowRunId))
            {
                continuation.FlowRunId = _flowRunId;
            }

            continuation.Token = continuation.Token ?? _token;
            continuation.Variables = continuation.Variables ?? _variables;
            if ((continuation.TriggerInputs == null || continuation.TriggerInputs.Count == 0) && _triggerInputs != null)
            {
                continuation.TriggerInputs = new Dictionary<string, object>(_triggerInputs, StringComparer.OrdinalIgnoreCase);
            }

            return _inner.DispatchAsync(continuation, cancellationToken);
        }
    }
}
