using System.Collections.Generic;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 运行态节点定义，保存节点类型、配置和执行策略。
    /// </summary>
    public sealed class NodeDefinition
    {
        public NodeDefinition()
        {
            Settings = new Dictionary<string, NodeSettingValue>(System.StringComparer.OrdinalIgnoreCase);
            ExecutionPolicy = new NodeExecutionPolicy();
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

        /// <summary>
        /// 节点执行策略，统一描述超时、重试、并发限制和失败后的流程走向。
        /// </summary>
        public NodeExecutionPolicy ExecutionPolicy { get; set; }
    }
}
