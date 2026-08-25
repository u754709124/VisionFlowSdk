using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// 按完整流程定义和节点实例配置生成描述符，用于依赖流程级变量定义的动态节点。
    /// </summary>
    public interface IFlowDefinitionNodeDescriptorProvider
    {
        /// <summary>解析节点在指定流程定义中的实际描述符。</summary>
        NodeDescriptor GetDescriptor(
            RuntimeFlowDefinition flow,
            NodeDefinition definition);
    }
}
