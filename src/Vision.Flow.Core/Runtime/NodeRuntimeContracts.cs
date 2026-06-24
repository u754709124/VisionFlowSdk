using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core
{
    public interface IFlowNode
    {
        Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken);
    }

    public sealed class NodeExecutionResult
    {
        public NodeExecutionResult()
        {
            OutputPort = "Next";
            Outputs = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public bool IsTimeout { get; set; }

        public string OutputPort { get; set; }

        public string ErrorMessage { get; set; }

        public Dictionary<string, object> Outputs { get; set; }

        public static NodeExecutionResult Success(string outputPort = "Next", IDictionary<string, object> outputs = null)
        {
            return new NodeExecutionResult
            {
                IsSuccess = true,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? "Next" : outputPort,
                Outputs = outputs == null ? new Dictionary<string, object>() : new Dictionary<string, object>(outputs)
            };
        }

        public static NodeExecutionResult Failure(string errorMessage, string outputPort = "Error")
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? "Error" : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>()
            };
        }

        public static NodeExecutionResult Timeout(string errorMessage = null, string outputPort = "Error")
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                IsTimeout = true,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? "Error" : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>()
            };
        }
    }

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
        }

        public RuntimeFlowDefinition Flow { get; private set; }

        public NodeDefinition Node { get; private set; }

        public FlowToken Token { get; private set; }

        public IVariablePool Variables { get; private set; }

        public IFlowEventSink Events { get; private set; }

        public IDeviceRegistry Devices { get; private set; }

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
}
