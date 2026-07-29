using System;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 节点固定值候选项。DisplayName 只用于界面展示，Value 是写入流程文件的稳定协议值。
    /// </summary>
    public sealed class NodeSettingConstantOption
    {
        public NodeSettingConstantOption(string value, string displayName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("候选项协议值不能为空。", "value");

            Value = value.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? Value
                : displayName.Trim();
        }

        public string Value { get; private set; }

        public string DisplayName { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
