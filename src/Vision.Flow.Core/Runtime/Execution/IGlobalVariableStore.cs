using System.Collections.Generic;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 提供单个 FlowRunner 独占的类型安全全局变量读写和原子快照能力。
    /// </summary>
    public interface IGlobalVariableStore
    {
        /// <summary>按稳定变量 Id 读取当前值。</summary>
        object Get(string variableId);

        /// <summary>按稳定变量 Id 写入与定义类型兼容的新值。</summary>
        void Set(string variableId, object value);

        /// <summary>在同一临界区内复制指定变量的当前值。</summary>
        IReadOnlyDictionary<string, object> CreateSnapshot(IEnumerable<string> variableIds);
    }
}
