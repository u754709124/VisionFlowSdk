using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// �˿���Ϣģ�ͣ������˿�������֮�䴫�ݵ�λ���ɼ����ɨ���������ġ�
    /// </summary>
    public sealed class MotionMessage
    {
        public MotionMessage()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string MessageType { get; set; }

        public string MotionId { get; set; }

        public string PositionId { get; set; }

        public string CaptureGroupId { get; set; }

        public string ScanGroupId { get; set; }

        public string TokenId { get; set; }

        public object Result { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
