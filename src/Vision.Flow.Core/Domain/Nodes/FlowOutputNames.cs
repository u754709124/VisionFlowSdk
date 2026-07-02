namespace Vision.Flow.Core.Domain.Nodes
{
    /// <summary>
    /// 节点输出变量名常量。变量绑定表达式依赖这些名称定位上游节点输出。
    /// </summary>
    public static class FlowOutputNames
    {
        public const string Result = "Result";
        public const string Value = "Value";
        public const string VariableName = "VariableName";
        public const string DelayMs = "DelayMs";
        public const string IsMatched = "IsMatched";
        public const string JoinKey = "JoinKey";
        public const string ActualInputCount = "ActualInputCount";
        public const string ExpectedInputCount = "ExpectedInputCount";
        public const string Image = "Image";
        public const string Frame = "Frame";
        public const string FrameId = "FrameId";
        public const string GrabTime = "GrabTime";
        public const string Metadata = "Metadata";
        public const string CameraId = "CameraId";
        public const string TriggerId = "TriggerId";
        public const string Frames = "Frames";
        public const string FrameCount = "FrameCount";
        public const string ScanGroupId = "ScanGroupId";
        public const string CaptureGroupId = "CaptureGroupId";
        public const string ShotIndex = "ShotIndex";
        public const string FrameIndex = "FrameIndex";
        public const string FrameGroup = "FrameGroup";
        public const string ScanGroup = "ScanGroup";
        public const string StitchedImage = "StitchedImage";
        public const string ImagePath = "ImagePath";
        public const string ResultImagePath = "ResultImagePath";
        public const string Saved = "Saved";
        public const string IsOk = "IsOk";
        public const string ResultImage = "ResultImage";
        public const string ElapsedMs = "ElapsedMs";
        public const string Queued = "Queued";
        public const string QueueCompleted = "QueueCompleted";
        public const string QueueDropped = "QueueDropped";
        public const string QueueNotifyOnly = "QueueNotifyOnly";
        public const string ImageQueued = "ImageQueued";
        public const string ResultImageQueued = "ResultImageQueued";
        public const string PreprocessResult = "PreprocessResult";
        public const string FramePreprocessResult = "FramePreprocessResult";
        public const string PreprocessedImage = "PreprocessedImage";
        public const string ScanGroupResult = "ScanGroupResult";
        public const string Final3DImage = "Final3DImage";
        public const string Final2DImage = "Final2DImage";
        public const string SourceFrameCount = "SourceFrameCount";
        public const string HeightMap = "HeightMap";
        public const string TextureImage = "TextureImage";
        public const string ConfidenceMap = "ConfidenceMap";
    }
}
