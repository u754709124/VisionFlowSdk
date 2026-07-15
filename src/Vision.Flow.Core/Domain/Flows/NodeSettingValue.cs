using System;
using System.Collections.Generic;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 节点配置值的取值模式：固定常量或运行时变量。
    /// </summary>
    public enum NodeSettingValueMode
    {
        Constant = 0,
        Variable = 1
    }

    /// <summary>
    /// 结构化变量选择器支持的数据来源范围。
    /// </summary>
    public enum VariableSelectorScope
    {
        NodeOutput = 0,
        TriggerInput = 1,
        Token = 2
    }

    /// <summary>
    /// 使用来源范围和路径描述一个运行时变量，不依赖字符串表达式。
    /// </summary>
    public sealed class VariableSelector
    {
        public VariableSelector()
        {
            Path = new List<string>();
        }

        public VariableSelectorScope Scope { get; set; }

        public List<string> Path { get; set; }

        public static VariableSelector ForNodeOutput(string nodeId, string outputName)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                throw new ArgumentException("Source node id is required.", "nodeId");
            }

            if (string.IsNullOrWhiteSpace(outputName))
            {
                throw new ArgumentException("Source output name is required.", "outputName");
            }

            return new VariableSelector
            {
                Scope = VariableSelectorScope.NodeOutput,
                Path = new List<string> { nodeId, outputName }
            };
        }

        public static VariableSelector ForToken(params string[] path)
        {
            return new VariableSelector
            {
                Scope = VariableSelectorScope.Token,
                Path = path == null ? new List<string>() : new List<string>(path)
            };
        }

        /// <summary>
        /// 创建入口输入选择器；首段是入口输入协议键，后续段用于读取对象成员。
        /// </summary>
        public static VariableSelector ForTriggerInput(params string[] path)
        {
            return new VariableSelector
            {
                Scope = VariableSelectorScope.TriggerInput,
                Path = path == null ? new List<string>() : new List<string>(path)
            };
        }
    }

    /// <summary>
    /// 节点可编辑配置值，同时保留固定值和可选的运行时变量选择器。
    /// </summary>
    public sealed class NodeSettingValue
    {
        public NodeSettingValue()
        {
            Mode = NodeSettingValueMode.Constant;
        }

        public NodeSettingValueMode Mode { get; set; }

        public object ConstantValue { get; set; }

        public VariableSelector Selector { get; set; }

        public static NodeSettingValue ForConstant(object value)
        {
            return new NodeSettingValue
            {
                Mode = NodeSettingValueMode.Constant,
                ConstantValue = value
            };
        }

        public static NodeSettingValue ForVariable(VariableSelector selector, object constantValue = null)
        {
            if (selector == null)
            {
                throw new ArgumentNullException("selector");
            }

            return new NodeSettingValue
            {
                Mode = NodeSettingValueMode.Variable,
                ConstantValue = constantValue,
                Selector = selector
            };
        }
    }
}
