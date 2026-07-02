using System;
using System.Globalization;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Nodes
{
    internal static class ControlFlowNodeHelpers
    {
        public static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = ResolveObject(context, name, defaultValue);
            return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
        {
            var value = ResolveObject(context, name, defaultValue);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static object ResolveObject(FlowExecutionContext context, string name, object defaultValue)
        {
            var value = context.GetInputValue(name);
            return value ?? defaultValue;
        }

        public static object ResolveBindingExpression(FlowExecutionContext context, string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var trimmed = expression.Trim();
            if (trimmed.StartsWith("{{", StringComparison.Ordinal) && trimmed.EndsWith("}}", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(2, trimmed.Length - 4).Trim();
            }

            if (trimmed.StartsWith("token.", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveTokenValue(context.Token, trimmed.Substring("token.".Length));
            }

            string sourceNodeId;
            string sourceOutputName;
            if (VariableBinding.TryParseVariablePath(expression, out sourceNodeId, out sourceOutputName))
            {
                return context.ResolveBinding(VariableBinding.ForExpression(expression));
            }

            return expression;
        }

        private static object ResolveTokenValue(FlowToken token, string name)
        {
            if (token == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (string.Equals(name, "TokenId", StringComparison.OrdinalIgnoreCase))
            {
                return token.TokenId;
            }

            if (string.Equals(name, "CreatedAtUtc", StringComparison.OrdinalIgnoreCase))
            {
                return token.CreatedAtUtc;
            }

            if (string.Equals(name, "ProductId", StringComparison.OrdinalIgnoreCase))
            {
                return token.ProductId;
            }

            if (string.Equals(name, "WorkpieceId", StringComparison.OrdinalIgnoreCase))
            {
                return token.WorkpieceId;
            }

            if (string.Equals(name, "PositionId", StringComparison.OrdinalIgnoreCase))
            {
                return token.PositionId;
            }

            if (string.Equals(name, "CaptureGroupId", StringComparison.OrdinalIgnoreCase))
            {
                return token.CaptureGroupId;
            }

            if (string.Equals(name, "ScanGroupId", StringComparison.OrdinalIgnoreCase))
            {
                return token.ScanGroupId;
            }

            if (string.Equals(name, "FrameId", StringComparison.OrdinalIgnoreCase))
            {
                return token.FrameId;
            }

            if (name.StartsWith("Metadata.", StringComparison.OrdinalIgnoreCase) && token.Metadata != null)
            {
                object value;
                return token.Metadata.TryGetValue(name.Substring("Metadata.".Length), out value) ? value : null;
            }

            if (name.StartsWith("Values.", StringComparison.OrdinalIgnoreCase) && token.Values != null)
            {
                object value;
                return token.Values.TryGetValue(name.Substring("Values.".Length), out value) ? value : null;
            }

            object tokenValue;
            return token.TryGet(name, out tokenValue) ? tokenValue : null;
        }
    }
}
