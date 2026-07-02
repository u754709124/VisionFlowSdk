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
    /// 图像保存结果，向流程返回保存状态、路径和附加元数据。
    /// </summary>
    public sealed class ImageSaveResult
    {
        public ImageSaveResult()
        {
            Metadata = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public string Path { get; set; }

        public string Message { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
