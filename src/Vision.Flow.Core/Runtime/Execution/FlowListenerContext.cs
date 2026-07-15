using System;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 流程监听节点启动上下文，提供设备、事件出口和后续调度器，不承载单次 Token 变量状态。
    /// </summary>
    public sealed class FlowListenerContext
    {
        public FlowListenerContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            IDeviceRegistry devices,
            IFlowEventSink events,
            IFlowContinuationDispatcher continuations)
            : this(flow, node, null, devices, events, continuations)
        {
        }

        public FlowListenerContext(
            RuntimeFlowDefinition flow,
            NodeDefinition node,
            FlowEntryDefinition entry,
            IDeviceRegistry devices,
            IFlowEventSink events,
            IFlowContinuationDispatcher continuations)
        {
            if (flow == null)
            {
                throw new ArgumentNullException("flow");
            }

            if (node == null)
            {
                throw new ArgumentNullException("node");
            }

            if (events == null)
            {
                throw new ArgumentNullException("events");
            }

            if (continuations == null)
            {
                throw new ArgumentNullException("continuations");
            }

            Flow = flow;
            Node = node;
            Entry = entry;
            Devices = devices ?? EmptyDeviceRegistry.Instance;
            Events = events;
            Continuations = continuations;
        }

        /// <summary>
        /// 当前运行态流程定义，监听节点用它定位自身所在流程。
        /// </summary>
        public RuntimeFlowDefinition Flow { get; private set; }

        /// <summary>
        /// 当前监听节点定义，不包含设计器视图状态。
        /// </summary>
        public NodeDefinition Node { get; private set; }

        /// <summary>
        /// 启动当前监听器的 NodeEvent 入口定义。
        /// </summary>
        public FlowEntryDefinition Entry { get; private set; }

        /// <summary>
        /// 运行时设备注册表，监听节点只能通过 Adapter 契约访问设备。
        /// </summary>
        public IDeviceRegistry Devices { get; private set; }

        /// <summary>
        /// 运行事件出口，用于报告监听节点启动、失败等运行态事件。
        /// </summary>
        public IFlowEventSink Events { get; private set; }

        /// <summary>
        /// 后续调度器，用于把外部事件转换为流程后续执行，避免占用设备回调线程。
        /// </summary>
        public IFlowContinuationDispatcher Continuations { get; private set; }
    }
}
