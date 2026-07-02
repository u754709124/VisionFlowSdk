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

namespace Vision.Flow.Core.Runtime
{
    /// <summary>
    /// 后台或流式节点向运行引擎请求继续调度指定输出端口的上下文。
    /// </summary>
    public sealed class FlowContinuation
    {
        public FlowContinuation()
        {
            OutputPort = FlowPortNames.Next;
            Outputs = new Dictionary<string, object>();
        }

        public string SourceNodeId { get; set; }

        public string OutputPort { get; set; }

        public FlowToken Token { get; set; }

        public IVariablePool Variables { get; set; }

        public IDictionary<string, object> Outputs { get; set; }

        public string FlowRunId { get; set; }
    }
}
