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

namespace Vision.Flow.Core.Descriptors
{
    /// <summary>
    /// 节点输出变量描述，供变量选择器和绑定校验使用。
    /// </summary>
    public sealed class NodeOutputDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string DataType { get; set; }

        public string Description { get; set; }
    }
}
