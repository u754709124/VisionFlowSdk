using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public sealed class ConditionNodeConfig
    {
        public ConditionNodeConfig()
        {
            Operator = ConditionOperator.Equal;
        }

        public string LeftBinding { get; set; }

        public ConditionOperator Operator { get; set; }

        public object RightValue { get; set; }

        public string RightBinding { get; set; }
    }
}
