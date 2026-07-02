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
    /// 相机参数描述，用于设计器或上位机呈现可配置参数。
    /// </summary>
    public sealed class CameraParameterDescriptor
    {
        public string ParameterName { get; set; }

        public string DisplayName { get; set; }

        public string ValueType { get; set; }

        public string Unit { get; set; }

        public bool IsWritable { get; set; }

        public object Minimum { get; set; }

        public object Maximum { get; set; }

        public object DefaultValue { get; set; }
    }
}
