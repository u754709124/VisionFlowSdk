using System;
using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ���֡����ģ�ͣ�����ͼ��֡�š��ɼ�ʱ���ƥ����Ԫ���ݡ�
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
