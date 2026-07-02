using System;
using System.Globalization;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 单次节点执行上下文，聚合流程定义、Token、变量池、设备、相机帧路由和事件出口。
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
            : this(flow, node, token, variables, events, devices, cameraFrames, null, null)
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
}
