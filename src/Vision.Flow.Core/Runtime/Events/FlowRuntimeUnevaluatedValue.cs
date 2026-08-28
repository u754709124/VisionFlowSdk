using System;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 表示调试采集明确排除求值、只保留类型的终止成员快照。
    /// </summary>
    public sealed class FlowRuntimeUnevaluatedValue
    {
        /// <summary>创建默认的 HTuple 未求值占位。</summary>
        public FlowRuntimeUnevaluatedValue()
        {
            Reason = "HTupleNotEvaluated";
        }

        /// <summary>获取或设置属性声明的完整 CLR 类型名称。</summary>
        public string TypeName { get; set; }

        /// <summary>获取或设置未求值原因的稳定协议值。</summary>
        public string Reason { get; set; }

        /// <summary>获取或设置属性声明类型是否具有资源语义。</summary>
        public bool IsResource { get; set; }
    }
}
