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
}
