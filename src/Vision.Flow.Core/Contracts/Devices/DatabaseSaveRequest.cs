using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 数据库保存请求，保存字段由公共节点根据变量绑定或映射配置组装。
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
