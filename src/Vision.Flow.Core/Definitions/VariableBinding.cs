using System;

namespace Vision.Flow.Core
{
    /// <summary>
    /// 变量绑定定义，描述节点输入如何从上游输出或常量值解析数据。
    /// </summary>
    public sealed class VariableBinding
    {
        /// <summary>
        /// 变量表达式文本，例如 `{{ camera_callback_1.Image }}`。
        /// </summary>
        public string Expression { get; set; }

        /// <summary>
        /// 解析后的上游节点 ID，便于校验器检查来源节点是否存在。
        /// </summary>
        public string SourceNodeId { get; set; }

        /// <summary>
        /// 解析后的上游输出名，便于校验器检查输出变量是否存在。
        /// </summary>
        public string SourceOutputName { get; set; }

        /// <summary>
        /// 常量绑定值，适合静态参数或调试输入。
        /// </summary>
        public object ConstantValue { get; set; }

        public string ValueType { get; set; }

        public bool IsConstant { get; set; }

        public static VariableBinding ForVariable(string sourceNodeId, string sourceOutputName)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId))
            {
                throw new ArgumentException("Source node id is required.", "sourceNodeId");
            }

            if (string.IsNullOrWhiteSpace(sourceOutputName))
            {
                throw new ArgumentException("Source output name is required.", "sourceOutputName");
            }

            return new VariableBinding
            {
                SourceNodeId = sourceNodeId,
                SourceOutputName = sourceOutputName,
                Expression = "{{ " + sourceNodeId + "." + sourceOutputName + " }}"
            };
        }

        public static VariableBinding ForExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new ArgumentException("Binding expression is required.", "expression");
            }

            var binding = new VariableBinding
            {
                Expression = expression
            };

            string sourceNodeId;
            string sourceOutputName;
            if (TryParseVariablePath(expression, out sourceNodeId, out sourceOutputName))
            {
                binding.SourceNodeId = sourceNodeId;
                binding.SourceOutputName = sourceOutputName;
            }

            return binding;
        }

        public static VariableBinding ForConstant(object value, string valueType = null)
        {
            return new VariableBinding
            {
                ConstantValue = value,
                IsConstant = true,
                ValueType = valueType
            };
        }

        public string GetVariableName()
        {
            if (!string.IsNullOrWhiteSpace(SourceNodeId) && !string.IsNullOrWhiteSpace(SourceOutputName))
            {
                return SourceNodeId + "." + SourceOutputName;
            }

            string sourceNodeId;
            string sourceOutputName;
            if (TryParseVariablePath(Expression, out sourceNodeId, out sourceOutputName))
            {
                return sourceNodeId + "." + sourceOutputName;
            }

            return null;
        }

        public static bool TryParseVariablePath(string expression, out string sourceNodeId, out string sourceOutputName)
        {
            sourceNodeId = null;
            sourceOutputName = null;

            if (string.IsNullOrWhiteSpace(expression))
            {
                return false;
            }

            var value = expression.Trim();
            if (value.StartsWith("{{", StringComparison.Ordinal) && value.EndsWith("}}", StringComparison.Ordinal))
            {
                value = value.Substring(2, value.Length - 4).Trim();
            }

            var splitIndex = value.LastIndexOf('.');
            if (splitIndex <= 0 || splitIndex >= value.Length - 1)
            {
                return false;
            }

            sourceNodeId = value.Substring(0, splitIndex).Trim();
            sourceOutputName = value.Substring(splitIndex + 1).Trim();
            return sourceNodeId.Length > 0 && sourceOutputName.Length > 0;
        }
    }
}
