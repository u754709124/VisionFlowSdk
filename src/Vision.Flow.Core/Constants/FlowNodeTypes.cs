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
    /// 流程节点类型常量。这里的值会进入流程文件和节点注册表，修改会破坏已发布流程兼容性。
    /// </summary>
    public static class FlowNodeTypes
    {
        public const string DelayWait = "delay.wait";
        public const string LogWrite = "log.write";
        public const string VariableSet = "variable.set";
        public const string FlowSplit = "flow.split";
        public const string JoinAnd = "join.and";
        public const string ConditionIf = "condition.if";
    }
}
