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
    /// 数据库保存请求，保存字段由公共节点根据变量绑定或映射配置组装。
    /// </summary>
    public sealed class DatabaseSaveRequest
    {
        public DatabaseSaveRequest()
        {
            Values = new Dictionary<string, object>();
            Metadata = new Dictionary<string, object>();
        }

        public string DatabaseId { get; set; }

        public string TableName { get; set; }

        public IDictionary<string, object> Values { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
