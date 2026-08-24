using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Core.Runtime.Engine
{
    /// <summary>
    /// 执行已发布流程，并统一管理监听器、FlowRun 生命周期、取消和停止排空。
    /// </summary>
    public sealed partial class FlowRunner : IFlowRunner, IFlowContinuationDispatcher
    {
        private readonly object _gate = new object();
        private readonly RuntimeFlowDefinition _definition;
        private readonly RuntimeFlowPlan _plan;
        private readonly NodeRegistry _nodeRegistry;
        private readonly IFlowEventSink _eventSink;
        private readonly IDeviceRegistry _devices;
        private readonly FlowExecutionOptions _options;
        private readonly Dictionary<string, IFlowNode> _nodeInstances;
        private readonly Dictionary<string, EntryExecutionGate> _entryGates;
        private readonly List<IFlowListenerNode> _startedListeners;
        private readonly Dictionary<string, ActiveFlowRun> _activeFlowRuns;
        private CancellationTokenSource _runnerCancellation;
        private Task _stopTask;
        private bool _isStopping;

        /// <summary>
        /// 使用默认设备注册表和执行选项创建流程运行器。
        /// </summary>
        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink = null)
            : this(definition, nodeRegistry, eventSink, null)
        {
        }

        /// <summary>
        /// 使用指定设备注册表创建流程运行器。
        /// </summary>
        public FlowRunner(RuntimeFlowDefinition definition, NodeRegistry nodeRegistry, IFlowEventSink eventSink, IDeviceRegistry devices)
            : this(definition, nodeRegistry, eventSink, devices, null)
        {
        }

        /// <summary>
        /// 使用完整依赖和执行选项创建流程运行器；运行器不拥有外部传入的设备注册表。
        /// </summary>
        public FlowRunner(
            RuntimeFlowDefinition definition,
            NodeRegistry nodeRegistry,
            IFlowEventSink eventSink,
            IDeviceRegistry devices,
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
            _eventSink = new SanitizingFlowEventSink(eventSink ?? new BoundedFlowEventSink());
            _devices = devices ?? EmptyDeviceRegistry.Instance;
            _options = CloneOptions(options);
            _options.EnvironmentVariableValues =
                EnvironmentVariableValues.CreateSnapshot(
                    definition.EnvironmentVariables,
                    _options.EnvironmentVariableValues);
            _nodeInstances = new Dictionary<string, IFlowNode>(StringComparer.OrdinalIgnoreCase);
            _entryGates = CreateEntryGates(definition.Entries);
            _startedListeners = new List<IFlowListenerNode>();
            _activeFlowRuns = new Dictionary<string, ActiveFlowRun>(StringComparer.OrdinalIgnoreCase);
            _stopTask = Task.FromResult(0);
        }

        /// <summary>
        /// 获取当前运行器使用的不可替换流程定义。
        /// </summary>
        public RuntimeFlowDefinition Definition
        {
            get { return _definition; }
        }

        /// <summary>
        /// 获取运行器是否已启动且仍接受新的 FlowRun。
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 获取构造时复制并规范化后的执行选项。
        /// </summary>
        public FlowExecutionOptions Options
        {
            get { return _options; }
        }

        /// <summary>
        /// 启动入口监听器并开放 FlowRun 准入。
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CancellationToken runnerToken;
            lock (_gate)
            {
                if (IsRunning)
                {
                    return;
                }

                if (_isStopping)
                {
                    throw new InvalidOperationException("FlowRunner cannot be started while StopAsync is draining active runs.");
                }

                _runnerCancellation = new CancellationTokenSource();
                IsRunning = true;
                runnerToken = _runnerCancellation.Token;
            }

            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, runnerToken))
            {
                try
                {
                    await StartListenerNodesAsync(linkedCancellation.Token).ConfigureAwait(false);
                    await PublishAsync(
                        FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStarted, _definition, null),
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    await StopStartedListenersAsync(CancellationToken.None).ConfigureAwait(false);
                    lock (_gate)
                    {
                        if (_runnerCancellation != null)
                        {
                            _runnerCancellation.Cancel();
                            _runnerCancellation.Dispose();
                            _runnerCancellation = null;
                        }

                        IsRunning = false;
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// 关闭准入、停止监听器、取消并排空全部 Active FlowRun；并发调用共享同一次停止过程。
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Task stopTask;
            lock (_gate)
            {
                if (_isStopping)
                {
                    stopTask = _stopTask;
                }
                else if (!IsRunning)
                {
                    stopTask = Task.FromResult(0);
                }
                else
                {
                    // 先关闭新 FlowRun 准入，再停止监听源；这样停止快照之后不会新增待排空运行。
                    IsRunning = false;
                    _isStopping = true;
                    stopTask = StopCoreAsync(_runnerCancellation);
                    _stopTask = stopTask;
                }
            }

            return WaitWithCancellationAsync(stopTask, cancellationToken);
        }

        private async Task StopCoreAsync(CancellationTokenSource cancellationSource)
        {
            // 避免在持有状态锁时同步进入宿主监听器的停止逻辑。
            await Task.Yield();
            Exception listenerError = null;
            try
            {
                await StopStartedListenersAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                listenerError = ex;
            }

            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
            }

            Task[] activeCompletions;
            lock (_gate)
            {
                activeCompletions = new Task[_activeFlowRuns.Count];
                var index = 0;
                foreach (var activeRun in _activeFlowRuns.Values)
                {
                    activeCompletions[index++] = activeRun.Completion;
                }
            }

            try
            {
                await Task.WhenAll(activeCompletions).ConfigureAwait(false);
                await PublishAsync(
                    FlowRuntimeEvent.Create(FlowRuntimeEventType.FlowStopped, _definition, null, null, NodeRuntimeState.Stopped),
                    CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                if (cancellationSource != null)
                {
                    cancellationSource.Dispose();
                }

                lock (_gate)
                {
                    if (ReferenceEquals(_runnerCancellation, cancellationSource))
                    {
                        _runnerCancellation = null;
                    }

                    _isStopping = false;
                }
            }

            if (listenerError != null)
            {
                throw listenerError;
            }
        }

        private static async Task WaitWithCancellationAsync(Task task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            var cancellation = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancellation.TrySetCanceled()))
            {
                var completed = await Task.WhenAny(task, cancellation.Task).ConfigureAwait(false);
                await completed.ConfigureAwait(false);
            }
        }
    }
}
