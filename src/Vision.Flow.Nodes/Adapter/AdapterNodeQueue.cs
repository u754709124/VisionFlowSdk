using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // 队列设置供可先接收任务再完成工作的重型适配器节点共享。
    public sealed class AdapterNodeQueueConfig
    {
        public AdapterNodeQueueConfig()
        {
            QueueName = "default";
            QueueCapacity = 16;
            QueueMaxDegreeOfParallelism = 1;
            QueueFullMode = "Wait";
            WaitForCompletion = true;
        }

        public bool UseQueue { get; set; }

        public string QueueName { get; set; }

        public int QueueCapacity { get; set; }

        public int QueueMaxDegreeOfParallelism { get; set; }

        public string QueueFullMode { get; set; }

        public bool WaitForCompletion { get; set; }
    }

    internal sealed class AdapterNodeQueueExecutionResult<T>
    {
        public bool WaitedForCompletion { get; set; }

        public bool IsQueued { get; set; }

        public bool IsDropped { get; set; }

        public bool IsNotifyOnly { get; set; }

        public T Value { get; set; }
    }
}
