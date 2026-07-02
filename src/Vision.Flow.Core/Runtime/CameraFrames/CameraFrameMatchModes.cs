namespace Vision.Flow.Core.Runtime.CameraFrames
{
    /// <summary>
    /// 相机帧匹配模式常量，用于相机回调节点和帧路由器之间协同过滤帧。
    /// </summary>
    public static class CameraFrameMatchModes
    {
        public const string TriggerId = "TriggerId";
        public const string Any = "Any";
        public const string TimeWindow = "TimeWindow";
        public const string ScanGroupId = "ScanGroupId";
    }
}
