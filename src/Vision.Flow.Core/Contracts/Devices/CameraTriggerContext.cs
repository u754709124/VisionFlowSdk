using System;
using System.Collections.Generic;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// �������������ģ����ڰ� TriggerId��Token ��ҵ��Ԫ���ݴ��������������
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
