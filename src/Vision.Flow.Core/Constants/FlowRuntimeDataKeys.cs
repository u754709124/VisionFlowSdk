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

namespace Vision.Flow.Core.Constants
{
    /// <summary>
    /// 运行事件 Data 字典键常量，供 Runtime、Demo 和测试读取运行时事件负载。
    /// </summary>
    public static class FlowRuntimeDataKeys
    {
        public const string VariableName = "VariableName";
        public const string Value = "Value";
        public const string QueueName = "QueueName";
        public const string QueueDepth = "QueueDepth";
        public const string Depth = "Depth";
        public const string Capacity = "Capacity";
        public const string MaxDegreeOfParallelism = "MaxDegreeOfParallelism";
        public const string FullMode = "FullMode";
        public const string TokenId = "TokenId";
        public const string NodeId = "NodeId";
        public const string NodeName = "NodeName";
        public const string OperationName = "OperationName";
        public const string FlowId = "FlowId";
        public const string ElapsedMs = "ElapsedMs";
        public const string Kind = "Kind";
        public const string LogLevel = "LogLevel";
        public const string Message = "Message";
    }
}
