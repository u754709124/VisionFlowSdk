using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core
{
    /// <summary>
    /// 输出端口扇出执行模式，控制一个输出端口连接多条边时的调度方式。
    /// </summary>
    public enum FlowFanOutMode
    {
        Sequential = 0,
        Parallel = 1
    }

    /// <summary>
    /// 分支 Token 处理模式，决定并行分支共享还是克隆运行上下文。
    /// </summary>
    public enum FlowBranchTokenMode
    {
        Shared = 0,
        Clone = 1
    }

    /// <summary>
    /// 流程执行选项，由生产运行时或设计器调试运行传入。
    /// </summary>
    public sealed class FlowExecutionOptions
    {
        public FlowExecutionOptions()
        {
            FanOutMode = FlowFanOutMode.Sequential;
            MaxDegreeOfParallelism = 1;
            BranchTokenMode = FlowBranchTokenMode.Shared;
        }

        public FlowFanOutMode FanOutMode { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public FlowBranchTokenMode BranchTokenMode { get; set; }

        public bool ContinueOnBranchFailure { get; set; }

        public int DefaultNodeTimeoutMs { get; set; }
    }

    /// <summary>
    /// 后台或流式节点向运行引擎请求继续调度指定输出端口的上下文。
    /// </summary>
    public sealed class FlowContinuation
    {
        public FlowContinuation()
        {
            OutputPort = FlowPortNames.Next;
            Outputs = new Dictionary<string, object>();
        }

        public string SourceNodeId { get; set; }

        public string OutputPort { get; set; }

        public FlowToken Token { get; set; }

        public IVariablePool Variables { get; set; }

        public IDictionary<string, object> Outputs { get; set; }

        public string FlowRunId { get; set; }
    }

    /// <summary>
    /// 继续执行调度器接口，由流程运行器实现以支持流式逐帧输出。
    /// </summary>
    public interface IFlowContinuationDispatcher
    {
        Task DispatchAsync(FlowContinuation continuation, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 运行时节点接口，所有公共节点通过该契约接入执行引擎。
    /// </summary>
    public interface IFlowNode
    {
        Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 节点执行结果，包含路由端口、输出变量和错误/超时状态。
    /// </summary>
    public sealed class NodeExecutionResult
    {
        public NodeExecutionResult()
        {
            OutputPort = FlowPortNames.Next;
            Outputs = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public bool IsTimeout { get; set; }

        public string OutputPort { get; set; }

        public string ErrorMessage { get; set; }

        public Dictionary<string, object> Outputs { get; set; }

        public static NodeExecutionResult Success(string outputPort = FlowPortNames.Next, IDictionary<string, object> outputs = null)
        {
            return new NodeExecutionResult
            {
                IsSuccess = true,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Next : outputPort,
                Outputs = outputs == null ? new Dictionary<string, object>() : new Dictionary<string, object>(outputs)
            };
        }

        public static NodeExecutionResult Failure(string errorMessage, string outputPort = FlowPortNames.Error)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Error : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>()
            };
        }

        public static NodeExecutionResult Timeout(string errorMessage = null, string outputPort = FlowPortNames.Error)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                IsTimeout = true,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Error : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>()
            };
        }
    }

    /// <summary>
    /// 单次节点执行上下文，聚合流程定义、Token、变量池、设备、队列和事件出口。
    /// </summary>
    public sealed class FlowExecutionContext
    {
        public FlowExecutionContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IFlowEventSink events)
            : this(flow, node, token, variables, events, null)
        {
        }

        public FlowExecutionContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IFlowEventSink events,
            IDeviceRegistry devices)
            : this(flow, node, token, variables, events, devices, null)
        {
        }

        public FlowExecutionContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IFlowEventSink events,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames)
            : this(flow, node, token, variables, events, devices, cameraFrames, null)
        {
        }

        public FlowExecutionContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IFlowEventSink events,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues)
            : this(flow, node, token, variables, events, devices, cameraFrames, queues, null, null)
        {
        }

        public FlowExecutionContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IFlowEventSink events,
            IDeviceRegistry devices,
            ICameraFrameRouter cameraFrames,
            IFlowTaskQueueRegistry queues,
            IFlowContinuationDispatcher continuations,
            string flowRunId)
        {
            if (flow == null)
            {
                throw new ArgumentNullException("flow");
            }

            if (node == null)
            {
                throw new ArgumentNullException("node");
            }

            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            if (variables == null)
            {
                throw new ArgumentNullException("variables");
            }

            if (events == null)
            {
                throw new ArgumentNullException("events");
            }

            Flow = flow;
            Node = node;
            Token = token;
            Variables = variables;
            Events = events;
            Devices = devices ?? EmptyDeviceRegistry.Instance;
            CameraFrames = cameraFrames ?? new DefaultCameraFrameRouter();
            Queues = queues ?? new FlowTaskQueueRegistry(events);
            Continuations = continuations ?? NullFlowContinuationDispatcher.Instance;
            FlowRunId = flowRunId;
        }

        public RuntimeFlowDefinition Flow { get; private set; }

        public NodeDefinition Node { get; private set; }

        public FlowToken Token { get; private set; }

        public IVariablePool Variables { get; private set; }

        public IFlowEventSink Events { get; private set; }

        public IDeviceRegistry Devices { get; private set; }

        public ICameraFrameRouter CameraFrames { get; private set; }

        public IFlowTaskQueueRegistry Queues { get; private set; }

        public IFlowContinuationDispatcher Continuations { get; private set; }

        public string FlowRunId { get; private set; }

        public object GetInputValue(string inputName)
        {
            if (string.IsNullOrWhiteSpace(inputName))
            {
                throw new ArgumentException("Input name is required.", "inputName");
            }

            VariableBinding binding;
            if (Node.InputBindings != null && Node.InputBindings.TryGetValue(inputName, out binding))
            {
                return ResolveBinding(binding);
            }

            object settingValue;
            if (Node.Settings != null && Node.Settings.TryGetValue(inputName, out settingValue))
            {
                return settingValue;
            }

            return null;
        }

        public T GetInputValue<T>(string inputName)
        {
            return ConvertValue<T>(GetInputValue(inputName), inputName);
        }

        public object ResolveBinding(VariableBinding binding)
        {
            if (binding == null)
            {
                return null;
            }

            if (binding.IsConstant)
            {
                return binding.ConstantValue;
            }

            var variableName = binding.GetVariableName();
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new InvalidOperationException("Variable binding does not contain a valid variable path.");
            }

            return Variables.Get(variableName);
        }

        private static T ConvertValue<T>(object value, string name)
        {
            if (value == null)
            {
                return default(T);
            }

            if (value is T)
            {
                return (T)value;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("Value '" + name + "' cannot be converted to " + typeof(T).FullName + ".", ex);
            }
        }
    }

    public interface IFlowRunner
    {
        RuntimeFlowDefinition Definition { get; }

        bool IsRunning { get; }

        Task StartAsync(CancellationToken cancellationToken = default(CancellationToken));

        Task StopAsync(CancellationToken cancellationToken = default(CancellationToken));

        Task TriggerAsync(string entryName, FlowToken token, CancellationToken cancellationToken = default(CancellationToken));
    }

    internal sealed class NullFlowContinuationDispatcher : IFlowContinuationDispatcher
    {
        public static readonly NullFlowContinuationDispatcher Instance = new NullFlowContinuationDispatcher();

        private NullFlowContinuationDispatcher()
        {
        }

        public Task DispatchAsync(FlowContinuation continuation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(0);
        }
    }
}
