using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public sealed class ConditionNodeFactory : BaseNodeFactory<ConditionNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.ConditionIf;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return ConditionNodeDescriptor.Create(); }
        }

        protected override ConditionNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new ConditionNodeConfig
            {
                LeftBinding = GetStringSetting(definition, FlowSettingNames.LeftBinding, null),
                Operator = GetEnumSetting(definition, FlowSettingNames.Operator, ConditionOperator.Equal),
                RightValue = GetSetting(definition, FlowSettingNames.RightValue, null),
                RightBinding = GetStringSetting(definition, FlowSettingNames.RightBinding, null)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, ConditionNodeConfig config)
        {
            return new ConditionNode(config);
        }
    }
}
