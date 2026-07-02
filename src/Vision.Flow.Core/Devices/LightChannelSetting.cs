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
    /// 光源通道设置，表达单个通道的开关、亮度和持续时间。
    /// </summary>
    public sealed class LightChannelSetting
    {
        public string LightId { get; set; }

        public string ChannelName { get; set; }

        public bool IsEnabled { get; set; }

        public double Intensity { get; set; }

        public int DurationMs { get; set; }
    }
}
