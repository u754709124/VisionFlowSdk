using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public sealed class ConditionNodeFactory : BaseNodeFactory<ConditionNodeConfig>
    {
        /// <summary>条件判断节点的稳定类型协议值。</summary>
        public const string TypeName = FlowNodeTypes.ConditionIf;

        /// <summary>获取条件判断节点的稳定类型协议值。</summary>
        public override string NodeType
        {
            get { return TypeName; }
        }

        /// <summary>获取条件判断节点的设计态和校验契约。</summary>
        public override NodeDescriptor Descriptor
        {
            get { return ConditionNodeDescriptor.Create(); }
        }

        protected override ConditionNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new ConditionNodeConfig
            {
                LeftValue = GetSetting(definition, FlowSettingNames.LeftValue, null),
                Operator = GetEnumSetting(definition, FlowSettingNames.Operator, ConditionOperator.Equal),
                RightValue = GetSetting(definition, FlowSettingNames.RightValue, null)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, ConditionNodeConfig config)
        {
            return new ConditionNode(config);
        }
    }
}
