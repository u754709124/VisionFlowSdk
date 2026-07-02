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
