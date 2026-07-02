using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Contracts.Nodes
{
    public interface INodeFactory
    {
        string NodeType { get; }

        NodeDescriptor Descriptor { get; }

        IFlowNode Create(NodeDefinition definition);
    }
}
