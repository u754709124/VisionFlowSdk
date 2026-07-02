using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public sealed class AndJoinNodeFactory : BaseNodeFactory<AndJoinNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.JoinAnd;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return AndJoinNodeDescriptor.Create(); }
        }

        protected override AndJoinNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new AndJoinNodeConfig
            {
                JoinKeyBinding = GetStringSetting(definition, "JoinKeyBinding", null),
                ExpectedInputCount = GetInt32Setting(definition, "ExpectedInputCount", 2),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 0),
                DuplicatePolicy = GetStringSetting(definition, "DuplicatePolicy", "Ignore")
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, AndJoinNodeConfig config)
        {
            return new AndJoinNode(config);
        }
    }
}
