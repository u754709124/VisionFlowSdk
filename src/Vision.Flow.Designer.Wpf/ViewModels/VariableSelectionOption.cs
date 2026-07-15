using System;
using System.Collections.Generic;
using System.Linq;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
    /// <summary>
    /// 设计器变量选择项；Selector 是持久化协议，其他字段仅供界面展示。
    /// </summary>
    public sealed class VariableSelectionOption
    {
        /// <summary>
        /// 创建一个变量候选；展示元数据不会写入流程文件。
        /// </summary>
        public VariableSelectionOption(
            VariableSelector selector,
            string groupName,
            string sourceName,
            string sourceId,
            string valueName,
            FlowDataType dataType)
        {
            Selector = selector;
            GroupName = groupName;
            SourceName = sourceName;
            SourceId = sourceId;
            ValueName = valueName;
            DataType = dataType;
        }

        /// <summary>
        /// 写入节点配置的结构化变量选择器。
        /// </summary>
        public VariableSelector Selector { get; private set; }

        /// <summary>
        /// 变量菜单分组名，通常为节点名称与节点 ID，Token 使用独立分组。
        /// </summary>
        public string GroupName { get; private set; }

        /// <summary>
        /// 来源节点或上下文的用户可读名称。
        /// </summary>
        public string SourceName { get; private set; }

        /// <summary>
        /// 来源节点 ID；Token 等非节点来源为空。
        /// </summary>
        public string SourceId { get; private set; }

        /// <summary>
        /// 输出或上下文字段的用户可读名称。
        /// </summary>
        public string ValueName { get; private set; }

        /// <summary>
        /// 候选值的数据类型，用于按目标配置类型过滤。
        /// </summary>
        public FlowDataType DataType { get; private set; }

        /// <summary>
        /// 返回包含来源、字段与类型的完整展示文本。
        /// </summary>
        public string DisplayText
        {
            get
            {
                var source = string.IsNullOrWhiteSpace(SourceId) || string.Equals(SourceName, SourceId, StringComparison.OrdinalIgnoreCase)
                    ? SourceName
                    : SourceName + " [" + SourceId + "]";
                return source + " / " + ValueName + " (" + FlowEnumConverter.ToWireValue(DataType) + ")";
            }
        }

        /// <summary>
        /// 返回适合按钮和节点卡片使用的紧凑展示文本。
        /// </summary>
        public string ShortDisplayText
        {
            get
            {
                return string.IsNullOrWhiteSpace(SourceId)
                    ? ValueName
                    : SourceId + "." + ValueName;
            }
        }

        /// <summary>
        /// 按范围和不区分大小写的路径判断是否指向同一变量。
        /// </summary>
        public bool Matches(VariableSelector selector)
        {
            if (Selector == null || selector == null || Selector.Scope != selector.Scope)
            {
                return false;
            }

            var left = Selector.Path ?? new List<string>();
            var right = selector.Path ?? new List<string>();
            return left.Count == right.Count && left.Zip(right, (x, y) =>
                string.Equals(x, y, StringComparison.OrdinalIgnoreCase)).All(x => x);
        }

        /// <summary>
        /// 将结构化选择器格式化为只读诊断文本。
        /// </summary>
        public static string FormatSelector(VariableSelector selector)
        {
            if (selector == null)
            {
                return "未选择变量";
            }

            var path = selector.Path == null ? string.Empty : string.Join(".", selector.Path.ToArray());
            return FlowEnumConverter.ToWireValue(selector.Scope) + (string.IsNullOrWhiteSpace(path) ? string.Empty : ": " + path);
        }
    }
}
