using System.Collections.Generic;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 表示普通非资源运行值的真实 CLR 类型及其已经脱离原对象的公开实例成员快照。
    /// </summary>
    public sealed class FlowRuntimeObjectSnapshot
    {
        /// <summary>初始化空成员快照，供运行事件生成和 JSON 反序列化使用。</summary>
        public FlowRuntimeObjectSnapshot()
        {
            Members = new Dictionary<string, object>();
        }

        /// <summary>获取或设置原始运行值的完整 CLR 类型名称。</summary>
        public string TypeName { get; set; }

        /// <summary>获取或设置原始对象是否实现资源释放契约；快照自身不拥有该资源。</summary>
        public bool IsResource { get; set; }

        /// <summary>获取或设置按名称稳定复制且不持有原对象的公开实例成员。</summary>
        public IDictionary<string, object> Members { get; set; }
    }
}
