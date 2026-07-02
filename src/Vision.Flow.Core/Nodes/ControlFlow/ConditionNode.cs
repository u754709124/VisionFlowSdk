using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    public sealed class ConditionNode : IFlowNode
    {
        private readonly ConditionNodeConfig _config;

        public ConditionNode(ConditionNodeConfig config)
        {
            _config = config ?? new ConditionNodeConfig();
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var leftBinding = ControlFlowNodeHelpers.ResolveString(context, "LeftBinding", _config.LeftBinding);
                if (string.IsNullOrWhiteSpace(leftBinding))
                {
                    return Task.FromResult(NodeExecutionResult.Failure("LeftBinding is required."));
                }

                var operatorName = ControlFlowNodeHelpers.ResolveString(context, "Operator", _config.Operator);
                if (string.IsNullOrWhiteSpace(operatorName))
                {
                    operatorName = "Equal";
                }

                var left = ControlFlowNodeHelpers.ResolveBindingExpression(context, leftBinding);
                var rightBinding = ControlFlowNodeHelpers.ResolveString(context, "RightBinding", _config.RightBinding);
                var right = string.IsNullOrWhiteSpace(rightBinding)
                    ? ControlFlowNodeHelpers.ResolveObject(context, "RightValue", _config.RightValue)
                    : ControlFlowNodeHelpers.ResolveBindingExpression(context, rightBinding);

                var isMatched = Evaluate(left, operatorName, right);
                return Task.FromResult(
                    NodeExecutionResult.Success(
                        isMatched ? "True" : "False",
                        new Dictionary<string, object>
                        {
                            { "Result", isMatched },
                            { "IsMatched", isMatched },
                            { "Left", left },
                            { "Right", right },
                            { "Operator", operatorName }
                        }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(NodeExecutionResult.Failure(ex.Message));
            }
        }

        private static bool Evaluate(object left, string operatorName, object right)
        {
            if (string.Equals(operatorName, "Equal", StringComparison.OrdinalIgnoreCase))
            {
                return ValuesEqual(left, right);
            }

            if (string.Equals(operatorName, "NotEqual", StringComparison.OrdinalIgnoreCase))
            {
                return !ValuesEqual(left, right);
            }

            if (string.Equals(operatorName, "GreaterThan", StringComparison.OrdinalIgnoreCase))
            {
                return Compare(left, right) > 0;
            }

            if (string.Equals(operatorName, "LessThan", StringComparison.OrdinalIgnoreCase))
            {
                return Compare(left, right) < 0;
            }

            if (string.Equals(operatorName, "Contains", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToString(left, CultureInfo.InvariantCulture)
                    .IndexOf(Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(operatorName, "IsNull", StringComparison.OrdinalIgnoreCase))
            {
                return left == null;
            }

            if (string.Equals(operatorName, "IsNotNull", StringComparison.OrdinalIgnoreCase))
            {
                return left != null;
            }

            throw new InvalidOperationException("Unsupported condition operator: " + operatorName);
        }

        private static bool ValuesEqual(object left, object right)
        {
            decimal leftDecimal;
            decimal rightDecimal;
            if (TryConvertDecimal(left, out leftDecimal) && TryConvertDecimal(right, out rightDecimal))
            {
                return leftDecimal == rightDecimal;
            }

            return string.Equals(
                Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);
        }

        private static int Compare(object left, object right)
        {
            decimal leftDecimal;
            decimal rightDecimal;
            if (TryConvertDecimal(left, out leftDecimal) && TryConvertDecimal(right, out rightDecimal))
            {
                return leftDecimal.CompareTo(rightDecimal);
            }

            return string.Compare(
                Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);
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
