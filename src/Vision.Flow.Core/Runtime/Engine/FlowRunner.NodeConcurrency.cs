using System;
using System.Collections.Generic;
using System.Threading;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Runtime.Engine
{
    public sealed partial class FlowRunner
    {
        private readonly Dictionary<string, SemaphoreSlim> _nodeExecutionGates =
            new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private SemaphoreSlim GetNodeExecutionGate(NodeDefinition node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id))
            {
                throw new InvalidOperationException("Node execution gate requires a stable NodeId.");
            }

            lock (_gate)
            {
                SemaphoreSlim executionGate;
                if (_nodeExecutionGates.TryGetValue(node.Id, out executionGate))
                {
                    return executionGate;
                }

                var policy = node.ExecutionPolicy ?? new NodeExecutionPolicy();
                var maxConcurrentExecutions = Math.Max(1, policy.MaxConcurrentExecutions);
                executionGate = new SemaphoreSlim(maxConcurrentExecutions, maxConcurrentExecutions);
                _nodeExecutionGates[node.Id] = executionGate;
                return executionGate;
            }
        }
    }
}
