using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Contracts.Nodes
{
    /// <summary>
    /// 按节点实例配置生成描述符的可选扩展契约。
    /// </summary>
    /// <remarks>
    /// 工厂的静态 Descriptor 仍用于节点库和新建节点默认值；只有端口、设置或输出会随实例配置变化时，
    /// 工厂才需要实现本接口。
    /// </remarks>
    public interface IInstanceNodeDescriptorProvider
    {
        NodeDescriptor GetDescriptor(NodeDefinition definition);
    }
}
