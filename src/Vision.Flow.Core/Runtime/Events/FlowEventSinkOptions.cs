namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 指定有界事件出口在遥测积压时采用的溢出策略。
    /// </summary>
    public enum FlowEventOverflowPolicy
    {
        /// <summary>
        /// 丢弃队列中最早的非关键遥测事件，为新遥测让出容量。
        /// </summary>
        DropOldest = 0,

        /// <summary>
        /// 丢弃当前新到达的非关键遥测事件。
        /// </summary>
        DropNewest = 1,

        /// <summary>
        /// 异步等待队列出现容量；关键生命周期事件始终采用此行为。
        /// </summary>
        Wait = 2
    }

    /// <summary>
    /// 配置事件队列容量、溢出行为和事件值快照的大小上限。
    /// </summary>
    public sealed class FlowEventSinkOptions
    {
        /// <summary>
        /// 获取或设置待转发事件的最大数量，默认值为 1024。
        /// </summary>
        public int Capacity { get; set; } = 1024;

        /// <summary>
        /// 获取或设置非关键遥测事件的溢出策略。
        /// </summary>
        public FlowEventOverflowPolicy OverflowPolicy { get; set; } = FlowEventOverflowPolicy.DropOldest;

        /// <summary>
        /// 获取或设置事件字符串快照允许保留的最大字符数。
        /// </summary>
        public int MaxStringLength { get; set; } = 512;

        /// <summary>
        /// 获取或设置字典、集合或普通对象快照允许保留的最大元素或公开成员数。
        /// </summary>
        public int MaxCollectionItems { get; set; } = 32;

        /// <summary>
        /// 获取或设置嵌套事件数据允许展开的最大深度，默认值为 5 层，与调试查看器保持一致。
        /// </summary>
        public int MaxDataDepth { get; set; } = 5;
    }
}
