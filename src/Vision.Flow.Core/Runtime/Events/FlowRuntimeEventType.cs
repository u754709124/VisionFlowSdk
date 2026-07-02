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

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 运行时事件类型，生产上位机和设计器调试面板通过它观察流程状态。
    /// </summary>
    public enum FlowRuntimeEventType
    {
        FlowStarted = 0,
        FlowStopped = 1,
        TokenCreated = 2,
        NodeWaiting = 3,
        NodeStarted = 4,
        NodeCompleted = 5,
        NodeFailed = 6,
        NodeTimeout = 7,
        OutputProduced = 8,
        ImageProduced = 9,
        QueueWarning = 10,
        QueueEnqueued = 11,
        QueueStarted = 12,
        QueueCompleted = 13,
        QueueFailed = 14,
        QueueRejected = 15
    }
}
