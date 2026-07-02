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
