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

namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 运行任务队列配置，控制容量、并发度和满载策略。
    /// </summary>
    public sealed class FlowTaskQueueOptions
    {
        public FlowTaskQueueOptions()
        {
            QueueName = FlowQueueNames.Default;
            Capacity = 16;
            MaxDegreeOfParallelism = 1;
            FullMode = FlowTaskQueueFullMode.Wait;
        }

        public string QueueName { get; set; }

        public int Capacity { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public FlowTaskQueueFullMode FullMode { get; set; }
    }
}
