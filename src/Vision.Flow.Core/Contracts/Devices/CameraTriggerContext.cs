using System;
using System.Collections.Generic;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 相机软触发上下文，用于把 TriggerId、Token 和业务元数据传给相机适配器。
    /// </summary>
    public sealed class CameraTriggerContext
    {
        public CameraTriggerContext()
        {
            TriggerId = Guid.NewGuid().ToString("N");
            Metadata = new Dictionary<string, object>();
        }

        public string CameraId { get; set; }

        public string TriggerId { get; set; }

        public FlowToken Token { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
