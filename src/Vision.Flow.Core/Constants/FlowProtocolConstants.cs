namespace Vision.Flow.Core
{
    /// <summary>
    /// 流程节点类型常量。这里的值会进入流程文件和节点注册表，修改会破坏已发布流程兼容性。
    /// </summary>
    public static class FlowNodeTypes
    {
        public const string DelayWait = "delay.wait";
        public const string LogWrite = "log.write";
        public const string VariableSet = "variable.set";
        public const string FlowSplit = "flow.split";
        public const string JoinAnd = "join.and";
        public const string ConditionIf = "condition.if";
    }

    /// <summary>
    /// 节点类型前缀常量，主要用于设计器按节点族展示颜色和图标。
    /// </summary>
    public static class FlowNodeTypePrefixes
    {
        public const string Camera = "camera.";
        public const string Light = "light.";
        public const string Database = "database.";
        public const string Join = "join.";
        public const string Group = "group.";
        public const string Scan = "scan.";
        public const string Fusion = "fusion.";
    }

    /// <summary>
    /// 控制流端口名常量。端口名会出现在连线和运行态调度中，必须保持稳定。
    /// </summary>
    public static class FlowPortNames
    {
        public const string In = "In";
        public const string Next = "Next";
        public const string Error = "Error";
        public const string Timeout = "Timeout";
        public const string Waiting = "Waiting";
        public const string True = "True";
        public const string False = "False";
        public const string Completed = "Completed";
        public const string Frame = "Frame";
    }

    /// <summary>
    /// 端口方向常量，供 Descriptor、设计器端口渲染和连线命中测试共用。
    /// </summary>
    public static class FlowPortDirections
    {
        public const string Input = "Input";
        public const string Output = "Output";
    }

    /// <summary>
    /// 节点描述中使用的数据类型名称。它们主要服务界面筛选和属性编辑，不代表运行时类型解析器。
    /// </summary>
    public static class FlowDataTypes
    {
        public const string Control = "Control";
        public const string String = "String";
        public const string Int32 = "Int32";
        public const string Int64 = "Int64";
        public const string Boolean = "Boolean";
        public const string Double = "Double";
        public const string Object = "Object";
        public const string DateTime = "DateTime";
        public const string IVisionImage = "IVisionImage";
        public const string CameraFrameData = "CameraFrameData";
        public const string RecipeRunResult = "RecipeRunResult";
    }

    /// <summary>
    /// 节点设置键常量。设置键会写入流程文件并被属性面板、节点工厂和校验器共同读取。
    /// </summary>
    public static class FlowSettingNames
    {
        public const string CameraId = "CameraId";
        public const string TriggerId = "TriggerId";
        public const string CallbackMode = "CallbackMode";
        public const string MatchMode = "MatchMode";
        public const string StreamOutputMode = "StreamOutputMode";
        public const string ScanGroupIdBinding = "ScanGroupIdBinding";
        public const string TimeoutMs = "TimeoutMs";
        public const string StableDelayMs = "StableDelayMs";
        public const string ExpectedFrameCount = "ExpectedFrameCount";
        public const string FrameTimeoutMs = "FrameTimeoutMs";
        public const string AutoStopAfterExpectedFrameCount = "AutoStopAfterExpectedFrameCount";
        public const string FrameIndexSource = "FrameIndexSource";
        public const string StartFrameIndex = "StartFrameIndex";
        public const string UseQueue = "UseQueue";
        public const string QueueName = "QueueName";
        public const string QueueCapacity = "QueueCapacity";
        public const string QueueMaxDegreeOfParallelism = "QueueMaxDegreeOfParallelism";
        public const string QueueFullMode = "QueueFullMode";
        public const string WaitForCompletion = "WaitForCompletion";
        public const string JoinKeyBinding = "JoinKeyBinding";
        public const string ExpectedInputCount = "ExpectedInputCount";
        public const string DuplicatePolicy = "DuplicatePolicy";
        public const string LeftBinding = "LeftBinding";
        public const string Operator = "Operator";
        public const string RightValue = "RightValue";
        public const string RightBinding = "RightBinding";
        public const string DelayMs = "DelayMs";
        public const string Message = "Message";
        public const string Level = "Level";
        public const string VariableName = "VariableName";
        public const string Value = "Value";
        public const string ValueBinding = "ValueBinding";
        public const string ConstantValue = "ConstantValue";
        public const string Binding = "Binding";
        public const string Expression = "Expression";
        public const string Name = "Name";
        public const string ParameterName = "ParameterName";
        public const string MotionId = "MotionId";
        public const string MessageType = "MessageType";
        public const string ResultBinding = "ResultBinding";
        public const string PositionName = "PositionName";
        public const string PositionId = "PositionId";
        public const string LightId = "LightId";
        public const string RecipeId = "RecipeId";
        public const string InputImage = "InputImage";
        public const string InputImageBinding = "InputImageBinding";
        public const string DatabaseId = "DatabaseId";
        public const string TableName = "TableName";
        public const string SaverId = "SaverId";
        public const string ImageSaverId = "ImageSaverId";
        public const string ImageBinding = "ImageBinding";
        public const string ResultImageBinding = "ResultImageBinding";
        public const string RootDirectory = "RootDirectory";
        public const string DirectoryTemplate = "DirectoryTemplate";
        public const string FileNameTemplate = "FileNameTemplate";
        public const string ImagePathTemplate = "ImagePathTemplate";
        public const string FieldMappings = "FieldMappings";
        public const string FieldName = "FieldName";
        public const string Field = "Field";
        public const string Column = "Column";
        public const string InputName = "InputName";
        public const string Input = "Input";
        public const string ChannelSettings = "ChannelSettings";
        public const string Channels = "Channels";
        public const string ChannelName = "ChannelName";
        public const string Channel = "Channel";
        public const string IsEnabled = "IsEnabled";
        public const string Enabled = "Enabled";
        public const string Intensity = "Intensity";
        public const string DurationMs = "DurationMs";
        public const string CaptureGroupId = "CaptureGroupId";
        public const string ScanGroupId = "ScanGroupId";
        public const string CaptureGroupIdBinding = "CaptureGroupIdBinding";
        public const string ShotIndexBinding = "ShotIndexBinding";
        public const string ExpectedShotCount = "ExpectedShotCount";
        public const string RequireContinuousShotIndex = "RequireContinuousShotIndex";
        public const string FirstShotIndex = "FirstShotIndex";
        public const string FrameBinding = "FrameBinding";
        public const string FrameIdBinding = "FrameIdBinding";
        public const string FrameIndexBinding = "FrameIndexBinding";
        public const string FrameIndex = "FrameIndex";
        public const string RequireContinuousFrameIndex = "RequireContinuousFrameIndex";
        public const string FirstFrameIndex = "FirstFrameIndex";
        public const string FrameGroupBinding = "FrameGroupBinding";
        public const string ScanGroupBinding = "ScanGroupBinding";
        public const string PreprocessedImageBinding = "PreprocessedImageBinding";
        public const string PreprocessResultBinding = "PreprocessResultBinding";
        public const string ScanGroupResultBinding = "ScanGroupResultBinding";
        public const string Parameters = "Parameters";
        public const string Disabled = "Disabled";
    }

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

    /// <summary>
    /// 图像、帧和 Token 元数据键常量。相机回调和组帧节点通过这些键交换工业视觉上下文。
    /// </summary>
    public static class FlowMetadataKeys
    {
        public const string CameraId = "CameraId";
        public const string TriggerId = "TriggerId";
        public const string TriggerTime = "TriggerTime";
        public const string ScanGroupId = "ScanGroupId";
        public const string CaptureGroupId = "CaptureGroupId";
        public const string ShotIndex = "ShotIndex";
        public const string FrameIndex = "FrameIndex";
        public const string TriggerIndex = "TriggerIndex";
        public const string Encoder = "Encoder";
        public const string ImageKind = "ImageKind";
        public const string Role = "Role";
        public const string Algorithm = "Algorithm";
        public const string TokenId = "TokenId";
        public const string NodeId = "NodeId";
        public const string SourceFrameCount = "SourceFrameCount";
        public const string FrameIndexes = "FrameIndexes";
        public const string ShotIndexes = "ShotIndexes";
        public const string ExpectedShotCount = "ExpectedShotCount";
        public const string ActualShotCount = "ActualShotCount";
        public const string ExpectedFrameCount = "ExpectedFrameCount";
        public const string ActualFrameCount = "ActualFrameCount";
        public const string SourceImageId = "SourceImageId";
        public const string FrameId = "FrameId";
        public const string GrabTime = "GrabTime";
        public const string SaverId = "SaverId";
        public const string ImageId = "ImageId";
        public const string ByteLength = "ByteLength";
        public const string HasNativeImage = "HasNativeImage";
        public const string PixelFormat = "PixelFormat";
    }

    /// <summary>
    /// 运行事件 Data 字典键常量，供 Runtime、Demo 和测试读取运行时事件负载。
    /// </summary>
    public static class FlowRuntimeDataKeys
    {
        public const string VariableName = "VariableName";
        public const string Value = "Value";
        public const string QueueName = "QueueName";
        public const string QueueDepth = "QueueDepth";
        public const string Depth = "Depth";
        public const string Capacity = "Capacity";
        public const string MaxDegreeOfParallelism = "MaxDegreeOfParallelism";
        public const string FullMode = "FullMode";
        public const string TokenId = "TokenId";
        public const string NodeId = "NodeId";
        public const string NodeName = "NodeName";
        public const string OperationName = "OperationName";
        public const string FlowId = "FlowId";
        public const string ElapsedMs = "ElapsedMs";
        public const string Kind = "Kind";
        public const string LogLevel = "LogLevel";
        public const string Message = "Message";
    }

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

    /// <summary>
    /// 相机回调模式常量，保持属性面板、节点校验和运行节点的枚举值一致。
    /// </summary>
    public static class CameraCallbackModes
    {
        public const string WaitNextFrame = "WaitNextFrame";
        public const string StreamFrames = "StreamFrames";
    }

    /// <summary>
    /// 流式帧输出模式常量。
    /// </summary>
    public static class CameraStreamOutputModes
    {
        public const string Batch = "Batch";
        public const string PerFrame = "PerFrame";
    }

    /// <summary>
    /// 帧索引来源常量。
    /// </summary>
    public static class FrameIndexSources
    {
        public const string Increment = "Increment";
        public const string Metadata = "Metadata";
    }

    /// <summary>
    /// 重复输入处理策略常量。
    /// </summary>
    public static class FlowDuplicatePolicies
    {
        public const string Error = "Error";
        public const string Ignore = "Ignore";
        public const string Replace = "Replace";
    }

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

    /// <summary>
    /// 流程校验错误码常量。外部工具和测试可能依赖这些错误码做自动判断。
    /// </summary>
    public static class FlowValidationIssueCodes
    {
        public const string FlowDesignMissing = "FlowDesignMissing";
        public const string RuntimeMissing = "RuntimeMissing";
        public const string FlowIdMissing = "FlowIdMissing";
        public const string NodesMissing = "NodesMissing";
        public const string NodeMissing = "NodeMissing";
        public const string NodeIdMissing = "NodeIdMissing";
        public const string NodeIdDuplicate = "NodeIdDuplicate";
        public const string NodeTypeMissing = "NodeTypeMissing";
        public const string NodeTypeNotRegistered = "NodeTypeNotRegistered";
        public const string NodeDescriptorMissing = "NodeDescriptorMissing";
        public const string NodeDescriptorTypeMissing = "NodeDescriptorTypeMissing";
        public const string NodeDescriptorTypeMismatch = "NodeDescriptorTypeMismatch";
        public const string EdgeMissing = "EdgeMissing";
        public const string EdgeSourceMissing = "EdgeSourceMissing";
        public const string EdgeTargetMissing = "EdgeTargetMissing";
        public const string EdgeFromPortMissing = "EdgeFromPortMissing";
        public const string EdgeToPortMissing = "EdgeToPortMissing";
        public const string EdgeSourcePortUnknown = "EdgeSourcePortUnknown";
        public const string EdgeToPortUnknown = "EdgeToPortUnknown";
        public const string EdgeTargetPortUnknown = "EdgeTargetPortUnknown";
        public const string EntriesMissing = "EntriesMissing";
        public const string EntryMissing = "EntryMissing";
        public const string EntryNameMissing = "EntryNameMissing";
        public const string EntryNameDuplicate = "EntryNameDuplicate";
        public const string EntryTargetMissing = "EntryTargetMissing";
        public const string EntryTargetNotFound = "EntryTargetNotFound";
        public const string RequiredSettingMissing = "RequiredSettingMissing";
        public const string BindingInvalid = "BindingInvalid";
        public const string BindingSourceNodeMissing = "BindingSourceNodeMissing";
        public const string BindingSourceMissing = "BindingSourceMissing";
        public const string BindingSourceNotFound = "BindingSourceNotFound";
        public const string BindingOutputMissing = "BindingOutputMissing";
        public const string RuntimeContainsViewState = "RuntimeContainsViewState";
        public const string CameraCallbackModeInvalid = "CameraCallbackModeInvalid";
        public const string CameraMatchModeInvalid = "CameraMatchModeInvalid";
        public const string CameraStreamOutputModeInvalid = "CameraStreamOutputModeInvalid";
        public const string QueueNameMissing = "QueueNameMissing";
        public const string QueueFullModeInvalid = "QueueFullModeInvalid";
        public const string DuplicatePolicyInvalid = "DuplicatePolicyInvalid";
        public const string SettingValueInvalid = "SettingValueInvalid";
    }

    /// <summary>
    /// 流程文件扩展名常量，用于区分设计态文件和生产运行态文件。
    /// </summary>
    public static class FlowFileExtensions
    {
        public const string FlowDesign = ".flowdesign";
        public const string FlowRuntime = ".flowruntime";
    }
}
