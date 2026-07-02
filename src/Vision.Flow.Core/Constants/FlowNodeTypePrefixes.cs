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
}
