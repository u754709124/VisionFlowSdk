namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 队列满载处理策略字符串常量。
    /// </summary>
    public static class FlowQueueFullModeNames
    {
        public const string Wait = "Wait";
        public const string Reject = "Reject";
        public const string Drop = "Drop";
        public const string StopFlow = "StopFlow";
        public const string NotifyOnly = "NotifyOnly";
    }
}
