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
}
