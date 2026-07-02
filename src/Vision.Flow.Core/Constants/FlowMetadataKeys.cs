using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Constants
{
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
}
