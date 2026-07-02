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
    /// 图像保存请求，描述待保存图像、目标路径和保存相关元数据。
    /// </summary>
    public sealed class ImageSaveRequest
    {
        public ImageSaveRequest()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string SaverId { get; set; }

        public IVisionImage Image { get; set; }

        public string DirectoryPath { get; set; }

        public string FileName { get; set; }

        public string Format { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
