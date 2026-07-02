namespace Vision.Flow.Core.Runtime.CameraFrames
{
    /// <summary>
    /// 相机回调节点处理帧数据的模式。
    /// </summary>
    public enum CameraCallbackMode
    {
        WaitNextFrame = 0,
        StreamFrames = 1
    }

    /// <summary>
    /// 等待相机帧时用于匹配帧与触发上下文的模式。
    /// </summary>
    public enum CameraFrameMatchMode
    {
        TriggerId = 0,
        Any = 1,
        TimeWindow = 2,
        ScanGroupId = 3
    }

    /// <summary>
    /// 流式相机帧输出的聚合方式。
    /// </summary>
    public enum CameraStreamOutputMode
    {
        Batch = 0,
        PerFrame = 1
    }

    /// <summary>
    /// 相机帧索引的来源。
    /// </summary>
    public enum FrameIndexSource
    {
        Increment = 0,
        Metadata = 1
    }
}
