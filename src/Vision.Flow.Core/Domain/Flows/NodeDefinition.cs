using System.Collections.Generic;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 运行态节点定义，保存节点类型、配置和变量绑定。
    /// </summary>
    public sealed class NodeDefinition
    {
        public NodeDefinition()
        {
            Settings = new Dictionary<string, NodeSettingValue>(System.StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 节点实例 ID，是变量名和连线引用的稳定键。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 节点类型，必须使用 `FlowNodeTypes` 中的已注册协议值。
        /// </summary>
        public string Type { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        /// <summary>
        /// 节点配置字典，键应优先使用 `FlowSettingNames` 常量。
        /// </summary>
        public Dictionary<string, NodeSettingValue> Settings { get; set; }
    }
}
