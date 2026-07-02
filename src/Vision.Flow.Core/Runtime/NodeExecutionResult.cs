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
    /// 节点执行结果，包含路由端口、输出变量和错误/超时状态。
    /// </summary>
    public sealed class NodeExecutionResult
    {
        public NodeExecutionResult()
        {
            OutputPort = FlowPortNames.Next;
            Outputs = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public bool IsTimeout { get; set; }

        public string OutputPort { get; set; }

        public string ErrorMessage { get; set; }

        public Dictionary<string, object> Outputs { get; set; }

        public static NodeExecutionResult Success(string outputPort = FlowPortNames.Next, IDictionary<string, object> outputs = null)
        {
            return new NodeExecutionResult
            {
                IsSuccess = true,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Next : outputPort,
                Outputs = outputs == null ? new Dictionary<string, object>() : new Dictionary<string, object>(outputs)
            };
        }

        public static NodeExecutionResult Failure(string errorMessage, string outputPort = FlowPortNames.Error)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Error : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>()
            };
        }

        public static NodeExecutionResult Timeout(string errorMessage = null, string outputPort = FlowPortNames.Error)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                IsTimeout = true,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Error : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>()
            };
        }
    }
}
