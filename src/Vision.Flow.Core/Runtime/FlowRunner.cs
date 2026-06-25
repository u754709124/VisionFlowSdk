using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core
{
    public sealed class FlowEngine
    {
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;
        private readonly ICameraFrameRouter _cameraFrames;
        private readonly IFlowTaskQueueRegistry _queues;
        private readonly FlowExecutionOptions _options;

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink = null)
            : this(nodeRegistry, eventSink, null, null)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IDeviceRegistry devices)
            : this(nodeRegistry, null, devices, null)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices)
            : this(nodeRegistry, eventSink, devices, null)
        {
        }

        public FlowEngine(NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices, ICameraFrameRouter cameraFrames)
            : this(nodeRegistry, eventSink, devices, cameraFrames, null)
        {
        }

        public FlowEngine(
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues)
            : this(nodeRegistry, eventSink, devices, cameraFrames, queues, null)
        {
        }

        public FlowEngine(
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues,
            FlowExecutionOptions options)
        {
            if (nodeRegistry == null)
            {
                throw new ArgumentNullException("nodeRegistry");
            }

            _nodeRegistry = nodeRegistry;
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
            _devices = devices ?? EmptyDeviceRegistry.Instance;
            _cameraFrames = cameraFrames ?? new DefaultCameraFrameRouter();
            _queues = queues ?? new FlowTaskQueueRegistry(_eventSink);
            _options = CloneOptions(options);
        }

        public IFlowRunner CreateRunner(RuntimeFlowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            return new FlowRunner(definition, _nodeRegistry, _eventSink, _devices, _cameraFrames, _queues, _options);
        }

        private static FlowExecutionOptions CloneOptions(FlowExecutionOptions options)
        {
            var source = options ?? new FlowExecutionOptions();
            return new FlowExecutionOptions
            {
                FanOutMode = source.FanOutMode,
                MaxDegreeOfParallelism = source.MaxDegreeOfParallelism <= 0 ? 1 : source.MaxDegreeOfParallelism,
                BranchTokenMode = source.BranchTokenMode,
                ContinueOnBranchFailure = source.ContinueOnBranchFailure,
                DefaultNodeTimeoutMs = source.DefaultNodeTimeoutMs
            };
        }
    }

    public sealed class FlowRunner : IFlowRunner, IFlowContinuationDispatcher
    {
        private readonly object _gate = new object();
        private readonly RuntimeFlowDefinition _definition;
        private readonly RuntimeFlowPlan _plan;
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;
        private readonly ICameraFrameRouter _cameraFrames;
        private readonly IFlowTaskQueueRegistry _queues;
        private readonly FlowExecutionOptions _options;
        private readonly Dictionary<string, IFlowNode> _nodeInstances;
        private CancellationTokenSource _runnerCancellation;

        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink = null)
            : this(definition, nodeRegistry, eventSink, null)
        {
        }

        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices)
            : this(definition, nodeRegistry, eventSink, devices, null)
        {
        }

        public FlowRunner(
            RuntimeFlowDefinition definition,
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames)
            : this(definition, nodeRegistry, eventSink, devices, cameraFrames, null)
        {
        }

        public FlowRunner(
            RuntimeFlowDefinition definition,
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues)
            : this(definition, nodeRegistry, eventSink, devices, cameraFrames, queues, null)
        {
        }

        public FlowRunner(
            RuntimeFlowDefinition definition,
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues,
            FlowExecutionOptions options)
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
            _cameraFrames = cameraFrames ?? new DefaultCameraFrameRouter();
            _queues = queues ?? new FlowTaskQueueRegistry(_eventSink);
            _options = CloneOptions(options);
            _nodeInstances = new Dictionary<string, IFlowNode>(StringComparer.OrdinalIgnoreCase);
        }

        public RuntimeFlowDefinition Definition
        {
            get { return _definition; }
        }

        public bool IsRunning { get; private set; }

        public FlowExecutionOptions Options
        {
            get { return _options; }
        }

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
                var outputPort = string.IsNullOrWhiteSpace(continuation.OutputPort) ? "Next" : continuation.OutputPort;
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

                if (result.Outputs != null && result.Outputs.ContainsKey("Image"))
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

        private async Task ExecuteGraphAsync(
            string nodeId,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
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
                var result = await ExecuteNodeAsync(node, token, variables, cancellationToken, flowRunId).ConfigureAwait(false);
                var outputPort = string.IsNullOrWhiteSpace(result.OutputPort) ? "Next" : result.OutputPort;
                await ExecuteOutgoingEdgesAsync(node, outputPort, token, variables, cancellationToken, currentPath, flowRunId)
                    .ConfigureAwait(false);
            }
            finally
            {
                currentPath.Remove(nodeId);
            }
        }

        private async Task ExecuteOutgoingEdgesAsync(
            NodeDefinition node,
            string outputPort,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
        {
            var outgoingEdges = _plan.GetOutgoingEdges(node.Id, outputPort);
            if (outgoingEdges.Count == 0)
            {
                return;
            }

            if (_options.FanOutMode != FlowFanOutMode.Parallel || outgoingEdges.Count == 1)
            {
                for (var index = 0; index < outgoingEdges.Count; index++)
                {
                    var edge = outgoingEdges[index];
                    if (edge == null)
                    {
                        continue;
                    }

                    await ExecuteGraphAsync(edge.ToNodeId, token, variables, cancellationToken, currentPath, flowRunId)
                        .ConfigureAwait(false);
                }

                return;
            }

            await ExecuteOutgoingEdgesInParallelAsync(outgoingEdges, token, variables, cancellationToken, currentPath, flowRunId)
                .ConfigureAwait(false);
        }

        private async Task ExecuteOutgoingEdgesInParallelAsync(
            IList<EdgeDefinition> outgoingEdges,
            FlowToken token,
            IVariablePool variables,
            CancellationToken cancellationToken,
            HashSet<string> currentPath,
            string flowRunId)
        {
            var maxDegree = _options.MaxDegreeOfParallelism <= 0 ? outgoingEdges.Count : _options.MaxDegreeOfParallelism;
            if (maxDegree <= 0)
            {
                maxDegree = 1;
            }

            using (var throttle = new SemaphoreSlim(maxDegree, maxDegree))
            {
                var tasks = new List<Task>();
                for (var index = 0; index < outgoingEdges.Count; index++)
                {
                    var edge = outgoingEdges[index];
                    if (edge == null)
                    {
                        continue;
                    }

                    await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
                    var branchPath = new HashSet<string>(currentPath, StringComparer.OrdinalIgnoreCase);
                    tasks.Add(Task.Run(
                        async delegate
                        {
                            try
                            {
                                await ExecuteGraphAsync(edge.ToNodeId, token, variables, cancellationToken, branchPath, flowRunId)
                                    .ConfigureAwait(false);
                            }
                            finally
                            {
                                throttle.Release();
                            }
                        },
                        cancellationToken));
                }

                if (_options.ContinueOnBranchFailure)
                {
                    for (var index = 0; index < tasks.Count; index++)
                    {
                        try
                        {
                            await tasks[index].ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
        }

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
                var context = new FlowExecutionContext(_definition, node, token, variables, _eventSink, _devices, _cameraFrames, _queues, this, flowRunId);
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
                        string.IsNullOrWhiteSpace(result.OutputPort) ? "Error" : result.OutputPort,
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
                        string.IsNullOrWhiteSpace(result.OutputPort) ? "Error" : result.OutputPort,
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
                    string.IsNullOrWhiteSpace(result.OutputPort) ? "Next" : result.OutputPort,
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

        private FlowRuntimeEvent CreateRuntimeEvent(
            FlowRuntimeEventType eventType,
            FlowToken token,
            NodeDefinition node,
            NodeRuntimeState state,
            string message,
            string outputPort,
            string flowRunId,
            long elapsedMs)
        {
            var runtimeEvent = FlowRuntimeEvent.Create(
                eventType,
                _definition,
                token,
                node,
                state,
                message,
                outputPort);
            runtimeEvent.FlowRunId = flowRunId;
            runtimeEvent.ElapsedMs = elapsedMs;
            if (elapsedMs > 0)
            {
                runtimeEvent.Data["ElapsedMs"] = elapsedMs;
            }

            return runtimeEvent;
        }

        private static FlowExecutionOptions CloneOptions(FlowExecutionOptions options)
        {
            var source = options ?? new FlowExecutionOptions();
            return new FlowExecutionOptions
            {
                FanOutMode = source.FanOutMode,
                MaxDegreeOfParallelism = source.MaxDegreeOfParallelism <= 0 ? 1 : source.MaxDegreeOfParallelism,
                BranchTokenMode = source.BranchTokenMode,
                ContinueOnBranchFailure = source.ContinueOnBranchFailure,
                DefaultNodeTimeoutMs = source.DefaultNodeTimeoutMs
            };
        }
    }
}
