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
    /// 节点端口描述，约束端口名、方向和控制流/数据类型。
    /// </summary>
    public sealed class NodePortDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Direction { get; set; }

        public string DataType { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }
}
