using System;
using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 运控事件参数，承载外部运控事件到流程触发所需的上下文。
    /// </summary>
    public sealed class MotionEventArgs : EventArgs
    {
        public MotionEventArgs()
        {
            TimestampUtc = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
        }

        public string MotionId { get; set; }

        public string EventType { get; set; }

        public string PositionId { get; set; }

        public string CaptureGroupId { get; set; }

        public string ScanGroupId { get; set; }

        public DateTime TimestampUtc { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
