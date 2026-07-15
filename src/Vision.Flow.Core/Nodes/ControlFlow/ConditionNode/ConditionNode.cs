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
                var left = ControlFlowNodeHelpers.ResolveObject(context, FlowSettingNames.LeftBinding, _config.LeftBinding);
                if (left == null)
                {
                    return Task.FromResult(NodeExecutionResult.Failure("Left value is required."));
                }

                var operatorName = ResolveOperator(context, _config.Operator);
                var operatorText = FlowEnumConverter.ToWireValue(operatorName);

                var right = context.Node.Settings != null && context.Node.Settings.ContainsKey(FlowSettingNames.RightBinding)
                    ? ControlFlowNodeHelpers.ResolveObject(context, FlowSettingNames.RightBinding, _config.RightBinding)
                    : ControlFlowNodeHelpers.ResolveObject(context, FlowSettingNames.RightValue, _config.RightValue);

                var isMatched = Evaluate(left, operatorName, right);
                return Task.FromResult(
                    NodeExecutionResult.Success(
                        isMatched ? FlowPortNames.True : FlowPortNames.False,
                        new Dictionary<string, object>
                        {
                            { FlowOutputNames.Result, isMatched },
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
            switch (operatorName)
            {
                case ConditionOperator.Equal:
                    return ValuesEqual(left, right);
                case ConditionOperator.NotEqual:
                    return !ValuesEqual(left, right);
                case ConditionOperator.GreaterThan:
                    return Compare(left, right) > 0;
                case ConditionOperator.LessThan:
                    return Compare(left, right) < 0;
                case ConditionOperator.Contains:
                    return Convert.ToString(left, CultureInfo.InvariantCulture)
                        .IndexOf(Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) >= 0;
                case ConditionOperator.IsNull:
                    return left == null;
                case ConditionOperator.IsNotNull:
                    return left != null;
                default:
                    throw new InvalidOperationException("Unsupported condition operator: " + FlowEnumConverter.ToWireValue(operatorName));
            }
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
