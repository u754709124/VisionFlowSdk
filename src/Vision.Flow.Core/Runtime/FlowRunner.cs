using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core
{
    public sealed class FlowEngine
    {
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink = null)
            : this(nodeRegistry, eventSink, null)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IDeviceRegistry devices)
            : this(nodeRegistry, null, devices)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices)
        {
            if (nodeRegistry == null)
            {
                throw new ArgumentNullException("nodeRegistry");
            }

            _nodeRegistry = nodeRegistry;
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
            _devices = devices ?? EmptyDeviceRegistry.Instance;
        }

        public IFlowRunner CreateRunner(RuntimeFlowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            return new FlowRunner(definition, _nodeRegistry, _eventSink, _devices);
        }
    }

    public sealed class FlowRunner : IFlowRunner
    {
        private readonly object _gate = new object();
        private readonly RuntimeFlowDefinition _definition;
        private readonly RuntimeFlowPlan _plan;
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;
        private readonly Dictionary<string, IFlowNode> _nodeInstances;
        private CancellationTokenSource _runnerCancellation;

        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink = null)
            : this(definition, nodeRegistry, eventSink, null)
        {
        }

        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            if (nodeRegistry == null)
            {
                throw new ArgumentNullException("nodeRegistry");
            }

            _definition = definition;
            _plan = new RuntimeFlowPlan(definition);
            _nodeRegistry = nodeRegistry;
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
            _devices = devices ?? EmptyDeviceRegistry.Instance;
            _nodeInstances = new Dictionary<string, IFlowNode>(StringComparer.OrdinalIgnoreCase);
        }

        public RuntimeFlowDefinition Definition
        {
            get { return _definition; }
        }

        public bool IsRunning { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (_gate)
            {
                if (IsRunning)
                {
                    return Task.FromResult(0);
                }

                _runnerCancellation = new CancellationTokenSource();
                IsRunning = true;
            }

            return PublishAsync(
                FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStarted, _definition, null),
                cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CancellationTokenSource cancellationSource = null;
            lock (_gate)
            {
                if (!IsRunning)
                {
                    return Task.FromResult(0);
                }

                cancellationSource = _runnerCancellation;
                _runnerCancellation = null;
                IsRunning = false;
            }

            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
            }

            return PublishAsync(
                FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStopped, _definition, null, null, NodeRuntimeState.Stopped),
                cancellationToken);
        }

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
                var variables = new VariablePool();
                await PublishAsync(
                    FlowRuntimeEvent.Create(FlowRuntimeEventType.TokenCreated, _definition, token),
                    linkedToken).ConfigureAwait(false);

                await ExecuteGraphAsync(
                    entry.TargetNodeId,
                    token,
                    variables,
                    linkedToken,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase)).ConfigureAwait(false);
            }
        }

        private async Task ExecuteGraphAsync(
            string nodeId,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken,
            HashSet<string> currentPath)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!currentPath.Add(nodeId))
            {
                throw new InvalidOperationException("Cycle detected while executing node: " + nodeId);
            }

            try
            {
                var node = FindNode(nodeId);
                var result = await ExecuteNodeAsync(node, token, variables, cancellationToken).ConfigureAwait(false);
                var outputPort = string.IsNullOrWhiteSpace(result.OutputPort) ? "Next" : result.OutputPort;
                var outgoingEdges = _plan.GetOutgoingEdges(node.Id, outputPort);
                for (var index = 0; index < outgoingEdges.Count; index++)
                {
                    var edge = outgoingEdges[index];
                    if (edge == null)
                    {
                        continue;
                    }

                    await ExecuteGraphAsync(edge.ToNodeId, token, variables, cancellationToken, currentPath)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                currentPath.Remove(nodeId);
            }
        }

        private async Task<NodeExecutionResult> ExecuteNodeAsync(
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken)
        {
            await PublishAsync(
                FlowRuntimeEvent.Create(FlowRuntimeEventType.NodeStarted, _definition, token, node, NodeRuntimeState.Running),
                cancellationToken).ConfigureAwait(false);

            NodeExecutionResult result;
            try
            {
                var flowNode = GetOrCreateNode(node);
                var context = new FlowExecutionContext(_definition, node, token, variables, _eventSink, _devices);
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
                await PublishAsync(
                    FlowRuntimeEvent.Create(
                        FlowRuntimeEventType.NodeTimeout,
                        _definition,
                        token,
                        node,
                        NodeRuntimeState.Timeout,
                        result.ErrorMessage,
                        string.IsNullOrWhiteSpace(result.OutputPort) ? "Error" : result.OutputPort),
                    cancellationToken).ConfigureAwait(false);
                return result;
            }

            if (!result.IsSuccess)
            {
                await PublishAsync(
                    FlowRuntimeEvent.Create(
                        FlowRuntimeEventType.NodeFailed,
                        _definition,
                        token,
                        node,
                        NodeRuntimeState.Failed,
                        result.ErrorMessage,
                        string.IsNullOrWhiteSpace(result.OutputPort) ? "Error" : result.OutputPort),
                    cancellationToken).ConfigureAwait(false);
                return result;
            }

            await WriteOutputsAsync(node, token, result, variables, cancellationToken).ConfigureAwait(false);
            await PublishAsync(
                FlowRuntimeEvent.Create(
                    FlowRuntimeEventType.NodeCompleted,
                    _definition,
                    token,
                    node,
                    NodeRuntimeState.Completed,
                    null,
                    string.IsNullOrWhiteSpace(result.OutputPort) ? "Next" : result.OutputPort),
                cancellationToken).ConfigureAwait(false);
            return result;
        }

        private async Task WriteOutputsAsync(
            NodeDefinition node,
            FlowToken token,
            NodeExecutionResult result,
            IVariablePool variables,
            CancellationToken cancellationToken)
        {
            if (result.Outputs == null)
            {
                return;
            }

            foreach (var output in result.Outputs)
            {
                var variableName = node.Id + "." + output.Key;
                variables.Set(variableName, output.Value);

                var runtimeEvent = FlowRuntimeEvent.Create(
                    FlowRuntimeEventType.OutputProduced,
                    _definition,
                    token,
                    node,
                    NodeRuntimeState.Completed,
                    null,
                    result.OutputPort);
                runtimeEvent.Data["VariableName"] = variableName;
                runtimeEvent.Data["Value"] = output.Value;
                await PublishAsync(runtimeEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        private FlowEntryDefinition FindEntry(string entryName)
        {
            FlowEntryDefinition entry;
            _plan.EntriesByName.TryGetValue(entryName, out entry);
            if (entry == null)
            {
                throw new ArgumentException("Flow entry was not found: " + entryName, "entryName");
            }

            if (string.IsNullOrWhiteSpace(entry.TargetNodeId))
            {
                throw new InvalidOperationException("Flow entry does not have a target node: " + entryName);
            }

            return entry;
        }

        private NodeDefinition FindNode(string nodeId)
        {
            NodeDefinition node;
            _plan.NodesById.TryGetValue(nodeId, out node);
            if (node == null)
            {
                throw new InvalidOperationException("Flow node was not found: " + nodeId);
            }

            return node;
        }

        private IFlowNode GetOrCreateNode(NodeDefinition node)
        {
            lock (_gate)
            {
                IFlowNode flowNode;
                if (!_nodeInstances.TryGetValue(node.Id, out flowNode))
                {
                    flowNode = _nodeRegistry.CreateNode(node);
                    _nodeInstances[node.Id] = flowNode;
                }

                return flowNode;
            }
        }

        private Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            return _eventSink.PublishAsync(runtimeEvent, cancellationToken);
        }
    }
}
