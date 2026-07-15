using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    /// <summary>
    /// 执行流分支节点配置，目前不需要额外参数。
    /// </summary>
    public sealed class SplitNodeConfig
    {
    }

    public sealed class SplitNodeFactory : BaseNodeFactory<SplitNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.FlowSplit;

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
            return Task.FromResult(NodeExecutionResult.Success(FlowPortNames.Next));
        }
    }

    public static class SplitNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = SplitNodeFactory.TypeName,
                DisplayName = "分支",
                Category = "通用",
                Version = "1.0.0",
                Description = "从默认分支继续执行流程。",
                InputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.In,
                        DisplayName = FlowPortNames.In,
                        Direction = FlowPortDirection.Input,
                        DataType = FlowDataType.Control,
                        IsRequired = true,
                        Description = "Execution input."
                    }
                },
                OutputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.Next,
                        DisplayName = FlowPortNames.Next,
                        Direction = FlowPortDirection.Output,
                        DataType = FlowDataType.Control,
                        Description = "Default output branch."
                    }
                }
            };
        }
    }
}
