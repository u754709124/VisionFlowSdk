using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    /// <summary>保存条件判断节点的默认操作数和比较操作符。</summary>
    public sealed class ConditionNodeConfig
    {
        public ConditionNodeConfig()
        {
            Operator = ConditionOperator.Equal;
        }

        /// <summary>获取或设置左操作数的默认值；正式流程必须通过 LeftValue 绑定变量。</summary>
        public object LeftValue { get; set; }

        /// <summary>获取或设置比较操作符。</summary>
        public ConditionOperator Operator { get; set; }

        /// <summary>获取或设置右操作数的默认固定值。</summary>
        public object RightValue { get; set; }
    }
}
