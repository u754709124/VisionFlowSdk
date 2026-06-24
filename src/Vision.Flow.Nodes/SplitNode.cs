using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    public sealed class SplitNodeConfig
    {
    }

    public sealed class SplitNodeFactory : BaseNodeFactory<SplitNodeConfig>
    {
        public const string TypeName = "flow.split";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return SplitNodeDescriptor.Create(); }
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, SplitNodeConfig config)
        {
            return new SplitNode(config);
        }
    }

    public sealed class SplitNode : IFlowNode
    {
        public SplitNode(SplitNodeConfig config)
        {
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(NodeExecutionResult.Success("Next"));
        }
    }

    public static class SplitNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = SplitNodeFactory.TypeName,
                DisplayName = "Split",
                Category = "Common",
                Version = "1.0.0",
                Description = "Routes execution through the default branch.",
                InputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "In",
                        DisplayName = "In",
                        Direction = "Input",
                        DataType = "Control",
                        IsRequired = true,
                        Description = "Execution input."
                    }
                },
                OutputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "Next",
                        DisplayName = "Next",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Default output branch."
                    }
                }
            };
        }
    }
}
