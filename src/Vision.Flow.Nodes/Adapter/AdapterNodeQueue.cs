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
    // Queue settings are shared by heavy adapter nodes that can accept work before completion.
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
