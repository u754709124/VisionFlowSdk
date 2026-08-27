using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    /// <summary>比较同类型的数值、字符串或枚举协议值，并选择真或假控制分支。</summary>
    public sealed class ConditionNode : IFlowNode
    {
        private readonly ConditionNodeConfig _config;

        /// <summary>使用条件节点的默认配置创建运行实例。</summary>
        public ConditionNode(ConditionNodeConfig config)
        {
            _config = config ?? new ConditionNodeConfig();
        }

        /// <summary>解析左右操作数并执行强类型条件比较。</summary>
        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var left = ControlFlowNodeHelpers.ResolveObject(context, FlowSettingNames.LeftValue, _config.LeftValue);
                if (left == null)
                {
                    return Task.FromResult(NodeExecutionResult.Failure("条件判断的左值不能为空。"));
                }

                var operatorName = ResolveOperator(context, _config.Operator);
                var operatorText = FlowEnumConverter.ToWireValue(operatorName);

                var right = ControlFlowNodeHelpers.ResolveObject(
                    context,
                    FlowSettingNames.RightValue,
                    _config.RightValue);
                if (right == null)
                {
                    return Task.FromResult(NodeExecutionResult.Failure("条件判断的右值不能为空。"));
                }

                var isMatched = Evaluate(left, operatorName, right);
                return Task.FromResult(
                    NodeExecutionResult.Success(
                        isMatched ? FlowPortNames.True : FlowPortNames.False,
                        new Dictionary<string, object>
                        {
                            { FlowOutputNames.IsMatched, isMatched },
                            { "Left", left },
                            { "Right", right },
                            { FlowSettingNames.Operator, operatorText }
                        }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(NodeExecutionResult.Failure(ex.Message));
            }
        }

        private static ConditionOperator ResolveOperator(FlowExecutionContext context, ConditionOperator defaultValue)
        {
            var value = context.GetSettingValue(FlowSettingNames.Operator);
            return FlowEnumConverter.ParseOrDefault(value, defaultValue);
        }

        private static bool Evaluate(object left, ConditionOperator operatorName, object right)
        {
            bool numeric = IsNumeric(left) && IsNumeric(right);
            bool textual = left is string && right is string;
            if (!numeric && !textual)
            {
                throw new InvalidOperationException(
                    "条件判断左右值必须同为数值，或同为字符串/枚举协议值。实际类型：" +
                    left.GetType().Name + " / " + right.GetType().Name + "。");
            }

            switch (operatorName)
            {
                case ConditionOperator.Equal:
                    return numeric
                        ? CompareNumbers(left, right) == 0
                        : string.Equals((string)left, (string)right, StringComparison.Ordinal);
                case ConditionOperator.NotEqual:
                    return numeric
                        ? CompareNumbers(left, right) != 0
                        : !string.Equals((string)left, (string)right, StringComparison.Ordinal);
                case ConditionOperator.LessThan:
                    EnsureNumericOperator(numeric, operatorName);
                    return CompareNumbers(left, right) < 0;
                case ConditionOperator.LessThanOrEqual:
                    EnsureNumericOperator(numeric, operatorName);
                    return CompareNumbers(left, right) <= 0;
                case ConditionOperator.GreaterThanOrEqual:
                    EnsureNumericOperator(numeric, operatorName);
                    return CompareNumbers(left, right) >= 0;
                case ConditionOperator.GreaterThan:
                    EnsureNumericOperator(numeric, operatorName);
                    return CompareNumbers(left, right) > 0;
                default:
                    throw new InvalidOperationException(
                        "不支持的条件操作符：" +
                        FlowEnumConverter.ToWireValue(operatorName) + "。");
            }
        }

        private static void EnsureNumericOperator(
            bool numeric,
            ConditionOperator operatorName)
        {
            if (!numeric)
            {
                throw new InvalidOperationException(
                    "操作符 " + FlowEnumConverter.ToWireValue(operatorName) +
                    " 只能用于数值比较。");
            }
        }

        private static int CompareNumbers(object left, object right)
        {
            decimal leftDecimal;
            decimal rightDecimal;
            if (!TryConvertDecimal(left, out leftDecimal) ||
                !TryConvertDecimal(right, out rightDecimal))
            {
                throw new InvalidOperationException(
                    "数值操作数无法转换为可比较的有限 Decimal 值。");
            }

            return leftDecimal.CompareTo(rightDecimal);
        }

        private static bool IsNumeric(object value)
        {
            if (value == null)
                return false;
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryConvertDecimal(object value, out decimal result)
        {
            result = 0;
            if (value == null)
            {
                return false;
            }

            try
            {
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
