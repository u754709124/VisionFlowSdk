using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 流程入口类型，决定入口由设计器、外部宿主还是监听节点触发。
    /// </summary>
    public enum FlowTriggerKind
    {
        Manual = 0,
        External = 1,
        NodeEvent = 2
    }

    /// <summary>
    /// 单个入口输入的稳定协议描述，供外部调用校验和设计器生成输入表单。
    /// </summary>
    public sealed class TriggerInputDescriptor
    {
        /// <summary>
        /// 输入协议键，流程文件和变量选择器通过该值引用输入。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 面向用户显示的可选标签；为空时界面回退到稳定协议键 Name。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 输入用途说明。
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 输入数据类型，用于发布校验和运行请求校验。
        /// </summary>
        public FlowDataType DataType { get; set; }

        /// <summary>
        /// 未提供输入且没有默认值时是否拒绝本次触发。
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// 调用方未提供输入时使用的默认值。
        /// </summary>
        public object DefaultValue { get; set; }
    }

    /// <summary>
    /// 入口级执行策略；容量只统计等待中的请求，不包含正在执行的请求。
    /// </summary>
    public sealed class TriggerExecutionPolicy
    {
        public TriggerExecutionPolicy()
        {
            MaxConcurrentRuns = 1;
            QueueCapacity = 64;
            QueueFullBehavior = TriggerQueueFullBehavior.Reject;
        }

        /// <summary>
        /// 同一入口允许同时执行的流程运行数，默认 1 表示入口内串行。
        /// </summary>
        public int MaxConcurrentRuns { get; set; }

        /// <summary>
        /// 同一入口允许等待的请求数；队列已满时直接拒绝新请求。
        /// </summary>
        public int QueueCapacity { get; set; }

        /// <summary>
        /// 队列满时的处理方式；当前协议只支持直接拒绝。
        /// </summary>
        public TriggerQueueFullBehavior QueueFullBehavior { get; set; }
    }

    /// <summary>
    /// 入口等待队列已满时的稳定处理协议。
    /// </summary>
    public enum TriggerQueueFullBehavior
    {
        Reject = 0
    }
}
