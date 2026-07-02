namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 运行队列名称常量，公共节点和设计器用它们保持默认队列名一致。
    /// </summary>
    public static class FlowQueueNames
    {
        public const string Default = "default";
        public const string Recipe = "recipe";
        public const string ImageSave = "image-save";
        public const string DatabaseSave = "database-save";
        public const string FramePreprocess = "frame-preprocess";
        public const string Fusion = "fusion";
    }
}
