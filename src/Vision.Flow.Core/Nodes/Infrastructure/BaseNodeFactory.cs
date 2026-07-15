using System;
using System.Globalization;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Nodes
{
    public abstract class BaseNodeFactory<TConfig> : INodeFactory
        where TConfig : class, new()
    {
        public abstract string NodeType { get; }

        public abstract NodeDescriptor Descriptor { get; }

        public IFlowNode Create(NodeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            return CreateNode(definition, CreateConfig(definition));
        }

        protected virtual TConfig CreateConfig(NodeDefinition definition)
        {
            return new TConfig();
        }

        protected abstract IFlowNode CreateNode(NodeDefinition definition, TConfig config);

        protected static object GetSetting(NodeDefinition definition, string name, object defaultValue)
        {
            NodeSettingValue value;
            if (definition.Settings != null)
            {
                foreach (var item in definition.Settings)
                {
                    if (string.Equals(item.Key, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = item.Value;
                        return value != null && value.Mode == NodeSettingValueMode.Constant
                            ? value.ConstantValue
                            : defaultValue;
                    }
                }
            }

            return defaultValue;
        }

        protected static string GetStringSetting(NodeDefinition definition, string name, string defaultValue)
        {
            var value = GetSetting(definition, name, defaultValue);
            return value == null ? null : Convert.ToString(value);
        }

        protected static int GetInt32Setting(NodeDefinition definition, string name, int defaultValue)
        {
            var value = GetSetting(definition, name, defaultValue);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        protected static TEnum GetEnumSetting<TEnum>(NodeDefinition definition, string name, TEnum defaultValue)
            where TEnum : struct
        {
            var value = GetSetting(definition, name, null);
            return FlowEnumConverter.ParseOrDefault(value, defaultValue);
        }
    }
}
