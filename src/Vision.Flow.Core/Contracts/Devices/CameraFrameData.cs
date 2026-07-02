using System;
using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 相机帧数据模型，承载图像、帧号、采集时间和匹配用元数据。
    /// </summary>
    public sealed class CameraFrameData
    {
        public CameraFrameData()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string CameraId { get; set; }

        public string TriggerId { get; set; }

        public string FrameId { get; set; }

        public DateTime GrabTime { get; set; }

        public IVisionImage Image { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
