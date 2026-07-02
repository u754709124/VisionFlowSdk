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

namespace Vision.Flow.Core.Devices
{
    /// <summary>
    /// 运控事件参数，承载外部运控事件到流程触发所需的上下文。
    /// </summary>
    public sealed class MotionEventArgs : EventArgs
    {
        public MotionEventArgs()
        {
            TimestampUtc = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
        }

        public string MotionId { get; set; }

        public string EventType { get; set; }

        public string PositionId { get; set; }

        public string CaptureGroupId { get; set; }

        public string ScanGroupId { get; set; }

        public DateTime TimestampUtc { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
