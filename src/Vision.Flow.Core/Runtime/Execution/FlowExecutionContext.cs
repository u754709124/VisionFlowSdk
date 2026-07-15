using System;
using System.Globalization;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 单次节点执行上下文，聚合流程定义、Token、变量池、设备、后续调度器和事件出口。
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
            : this(flow, node, token, variables, events, devices, null, null)
        {
        }

        public FlowExecutionContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            FlowToken token,
            IVariablePool variables,
            IFlowEventSink events,
            IDeviceRegistry devices,
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
            Continuations = continuations ?? NullFlowContinuationDispatcher.Instance;
            FlowRunId = flowRunId;
            SettingValueResolver = DefaultSettingValueResolver.Instance;
        }

        public RuntimeFlowDefinition Flow { get; private set; }

        public NodeDefinition Node { get; private set; }

        public FlowToken Token { get; private set; }

        public IVariablePool Variables { get; private set; }

        public IFlowEventSink Events { get; private set; }

        public IDeviceRegistry Devices { get; private set; }

        public IFlowContinuationDispatcher Continuations { get; private set; }

        public string FlowRunId { get; private set; }

        public ISettingValueResolver SettingValueResolver { get; private set; }

        public object GetSettingValue(string settingName)
        {
            if (string.IsNullOrWhiteSpace(settingName))
            {
                throw new ArgumentException("Setting name is required.", "settingName");
            }

            NodeSettingValue setting;
            if (!TryGetSettingValue(settingName, out setting) || setting == null)
            {
                return null;
            }

            if (setting.Mode == NodeSettingValueMode.Constant)
            {
                return setting.ConstantValue;
            }

            if (setting.Mode != NodeSettingValueMode.Variable || setting.Selector == null)
            {
                throw new InvalidOperationException("Setting '" + settingName + "' does not contain a valid variable selector.");
            }

            return SettingValueResolver.Resolve(setting.Selector, this);
        }

        public T GetSettingValue<T>(string settingName)
        {
            return ConvertValue<T>(GetSettingValue(settingName), settingName);
        }

        private bool TryGetSettingValue(string settingName, out NodeSettingValue setting)
        {
            setting = null;
            if (Node.Settings == null)
            {
                return false;
            }

            foreach (var item in Node.Settings)
            {
                if (string.Equals(item.Key, settingName, StringComparison.OrdinalIgnoreCase))
                {
                    setting = item.Value;
                    return true;
                }
            }

            return false;
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
