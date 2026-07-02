using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ���ݿⱣ�����󣬱����ֶ��ɹ����ڵ���ݱ����󶨻�ӳ��������װ��
    /// </summary>
    public sealed class DatabaseSaveRequest
    {
        public DatabaseSaveRequest()
        {
            Values = new Dictionary<string, object>();
            Metadata = new Dictionary<string, object>();
        }

        public string DatabaseId { get; set; }

        public string TableName { get; set; }

        public IDictionary<string, object> Values { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
