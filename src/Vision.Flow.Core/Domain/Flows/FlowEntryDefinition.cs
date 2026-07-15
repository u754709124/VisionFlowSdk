using System.Collections.Generic;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 流程入口定义，用于生产上位机从外部事件触发指定节点。
    /// </summary>
    public sealed class FlowEntryDefinition
    {
        public FlowEntryDefinition()
        {
            TriggerKind = FlowTriggerKind.Manual;
            Inputs = new List<TriggerInputDescriptor>();
            ExecutionPolicy = new TriggerExecutionPolicy();
        }

        /// <summary>
        /// 入口协议名，运行请求通过它选择入口。
        /// </summary>
        public string EntryName { get; set; }

        /// <summary>
        /// 入口开始执行的目标节点 ID。
        /// </summary>
        public string TargetNodeId { get; set; }

        /// <summary>
        /// NodeEvent 入口的监听源节点 ID；手动和外部入口不使用该字段。
        /// </summary>
        public string SourceNodeId { get; set; }

        /// <summary>
        /// 入口接受的触发来源类型。
        /// </summary>
        public FlowTriggerKind TriggerKind { get; set; }

        /// <summary>
        /// 入口输入协议；运行时会在进入队列前完成必填项和类型校验。
        /// </summary>
        public List<TriggerInputDescriptor> Inputs { get; set; }

        /// <summary>
        /// 入口级并发和排队策略。
        /// </summary>
        public TriggerExecutionPolicy ExecutionPolicy { get; set; }
    }
}
