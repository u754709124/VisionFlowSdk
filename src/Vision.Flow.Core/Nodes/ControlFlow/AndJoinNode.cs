using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    public sealed class AndJoinNode : IFlowNode
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, AndJoinBucket> _buckets = new Dictionary<string, AndJoinBucket>(StringComparer.OrdinalIgnoreCase);
        private readonly AndJoinNodeConfig _config;

        public AndJoinNode(AndJoinNodeConfig config)
        {
            _config = config ?? new AndJoinNodeConfig();
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedInputCount = ControlFlowNodeHelpers.ResolveInt32(context, "ExpectedInputCount", _config.ExpectedInputCount);
            if (expectedInputCount <= 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("ExpectedInputCount must be greater than zero."));
            }

            var timeoutMs = ControlFlowNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero."));
            }

            var joinKeyBinding = ControlFlowNodeHelpers.ResolveString(context, "JoinKeyBinding", _config.JoinKeyBinding);
            var joinKeyValue = ControlFlowNodeHelpers.ResolveBindingExpression(context, joinKeyBinding);
            var joinKey = joinKeyValue == null ? null : Convert.ToString(joinKeyValue, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(joinKey))
            {
                return Task.FromResult(NodeExecutionResult.Failure("JoinKeyBinding must resolve to a non-empty value."));
            }

            var duplicatePolicy = ControlFlowNodeHelpers.ResolveString(context, "DuplicatePolicy", _config.DuplicatePolicy);
            if (string.IsNullOrWhiteSpace(duplicatePolicy))
            {
                duplicatePolicy = "Ignore";
            }

            var inputKey = string.IsNullOrWhiteSpace(context.Token.TokenId)
                ? Guid.NewGuid().ToString("N")
                : context.Token.TokenId;

            lock (_gate)
            {
                AndJoinBucket bucket;
                if (!_buckets.TryGetValue(joinKey, out bucket))
                {
                    bucket = new AndJoinBucket(joinKey, expectedInputCount, timeoutMs);
                    _buckets[joinKey] = bucket;
                }

                bucket.ExpectedInputCount = expectedInputCount;
                bucket.TimeoutMs = timeoutMs;

                if (bucket.IsExpired())
                {
                    _buckets.Remove(joinKey);
                    return Task.FromResult(NodeExecutionResult.Timeout("AND join timed out for JoinKey: " + joinKey));
                }

                if (bucket.Inputs.ContainsKey(inputKey))
                {
                    if (string.Equals(duplicatePolicy, "Error", StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(NodeExecutionResult.Failure("Duplicate AND join input for JoinKey '" + joinKey + "' and TokenId '" + inputKey + "'."));
                    }

                    if (string.Equals(duplicatePolicy, "Replace", StringComparison.OrdinalIgnoreCase))
                    {
                        bucket.Inputs[inputKey] = DateTime.UtcNow;
                    }

                    return Task.FromResult(CreateWaitingResult(bucket));
                }

                bucket.Inputs[inputKey] = DateTime.UtcNow;
                if (bucket.Inputs.Count < expectedInputCount)
                {
                    return Task.FromResult(CreateWaitingResult(bucket));
                }

                _buckets.Remove(joinKey);
                return Task.FromResult(
                    NodeExecutionResult.Success(
                        "Next",
                        new Dictionary<string, object>
                        {
                            { "Result", true },
                            { "IsMatched", true },
                            { "JoinKey", joinKey },
                            { "ActualInputCount", bucket.Inputs.Count },
                            { "ExpectedInputCount", expectedInputCount }
                        }));
            }
        }

        private static NodeExecutionResult CreateWaitingResult(AndJoinBucket bucket)
        {
            return NodeExecutionResult.Success(
                "Waiting",
                new Dictionary<string, object>
                {
                    { "Result", false },
                    { "IsMatched", false },
                    { "JoinKey", bucket.JoinKey },
                    { "ActualInputCount", bucket.Inputs.Count },
                    { "ExpectedInputCount", bucket.ExpectedInputCount }
                });
        }
    }
}
