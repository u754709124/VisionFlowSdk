using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Designer.Wpf.Theming;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 入口辅助逻辑只维护设计器自动生成的监听入口，并提供无编辑能力的当前入口快照。
    public sealed partial class FlowDesignerControl
    {
        private const string AutomaticNodeEventEntryPrefix = "NodeEvent_";

        private void AddAutomaticListenerEntry(NodeDefinition node)
        {
            if (node == null || _document == null || _document.Runtime == null)
            {
                return;
            }

            INodeFactory factory;
            if (!_nodeRegistry.TryGetFactory(node.Type, out factory) || !(factory is IFlowListenerNodeFactory))
            {
                return;
            }

            _document.Runtime.Entries.Add(new FlowEntryDefinition
            {
                EntryName = CreateUniqueEntryName(AutomaticNodeEventEntryPrefix + node.Id),
                TriggerKind = FlowTriggerKind.NodeEvent,
                SourceNodeId = node.Id,
                TargetNodeId = null
            });
        }

        private string CreateUniqueEntryName(string preferredName)
        {
            var candidate = preferredName;
            var suffix = 2;
            while (_document.Runtime.Entries.Any(entry =>
                entry != null && string.Equals(entry.EntryName, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = preferredName + "_" + suffix;
                suffix++;
            }

            return candidate;
        }

        private void RemoveAutomaticListenerEntries(string nodeId)
        {
            if (_document == null || _document.Runtime == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            _document.Runtime.Entries.RemoveAll(entry =>
                entry != null &&
                entry.TriggerKind == FlowTriggerKind.NodeEvent &&
                string.Equals(entry.SourceNodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        }

        private void ShowEntryListDialog()
        {
            var dialog = new FlowDesignerDialogWindow(
                "流程入口列表",
                720,
                430,
                Window.GetWindow(this));
            dialog.DialogContent = CreateEntryListContent(delegate { dialog.DialogResult = false; });
            dialog.ShowDialog();
        }

        private FrameworkElement CreateEntryListContent(Action close)
        {
            var root = new Grid
            {
                Background = Brushes.White,
                Margin = new Thickness(18),
                Tag = "EntryListReadOnly"
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var entries = _document == null || _document.Runtime == null
                ? new FlowEntryDefinition[0]
                : (_document.Runtime.Entries ?? new System.Collections.Generic.List<FlowEntryDefinition>())
                    .Where(entry => entry != null)
                    .ToArray();
            if (entries.Length == 0)
            {
                root.Children.Add(new TextBlock
                {
                    Text = "当前流程没有入口。",
                    Tag = "EntryListEmptyState",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = BrushFromRgb(100, 116, 139),
                    FontSize = 14
                });
            }
            else
            {
                root.Children.Add(CreateEntryListTable(entries));
            }

            var closeButton = new Button
            {
                Content = "关闭",
                Tag = "EntryListClose",
                MinWidth = 86,
                Height = 32,
                Margin = new Thickness(0, 14, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.SetResourceReference(StyleProperty, FlowDesignerTheme.PrimaryButtonStyleKey);
            closeButton.Click += delegate
            {
                if (close != null)
                {
                    close();
                }
            };
            Grid.SetRow(closeButton, 1);
            root.Children.Add(closeButton);
            return root;
        }

        private FrameworkElement CreateEntryListTable(FlowEntryDefinition[] entries)
        {
            var table = new Grid();
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            AddEntryListCell(table, "入口节点名称", 0, 0, true);
            AddEntryListCell(table, "触发类型", 0, 1, true);
            AddEntryListCell(table, "节点 ID", 0, 2, true);

            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var nodeId = entry.TriggerKind == FlowTriggerKind.NodeEvent
                    ? entry.SourceNodeId
                    : entry.TargetNodeId;
                var node = _document.Runtime.Nodes.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(candidate.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                var nodeName = node == null
                    ? "节点不存在"
                    : string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name;
                var rowIndex = index + 1;
                table.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
                AddEntryListCell(table, nodeName, rowIndex, 0, false);
                AddEntryListCell(table, GetEntryTriggerKindDisplayName(entry.TriggerKind), rowIndex, 1, false);
                AddEntryListCell(table, string.IsNullOrWhiteSpace(nodeId) ? "-" : nodeId, rowIndex, 2, false);
            }

            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = table
            };
        }

        private static void AddEntryListCell(
            Grid table,
            string text,
            int row,
            int column,
            bool isHeader)
        {
            var border = new Border
            {
                Background = isHeader ? BrushFromRgb(245, 247, 250) : Brushes.White,
                BorderBrush = BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(column == 0 ? 1 : 0, row == 0 ? 1 : 0, 1, 1),
                Padding = new Thickness(10, 0, 10, 0),
                Child = new TextBlock
                {
                    Text = text ?? string.Empty,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = BrushFromRgb(51, 65, 85),
                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
            table.Children.Add(border);
        }

        private static string GetEntryTriggerKindDisplayName(FlowTriggerKind kind)
        {
            switch (kind)
            {
                case FlowTriggerKind.Manual:
                    return "手动";
                case FlowTriggerKind.External:
                    return "外部";
                case FlowTriggerKind.NodeEvent:
                    return "节点事件";
                default:
                    return "未知";
            }
        }
    }
}
