using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Vision.Flow.Core;
using Vision.Flow.Nodes;
using ShapesPath = System.Windows.Shapes.Path;

namespace Vision.Flow.Designer.Wpf
{
    // 节点库控件负责节点库条目渲染和新增节点请求。
    public sealed class NodePaletteControl : Border
    {
        private readonly StackPanel _items;
        private readonly List<Button> _descriptorButtons;
        private bool _isReadOnly;

        public NodePaletteControl()
        {
            _descriptorButtons = new List<Button>();
            Margin = new Thickness(0);
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            var layout = new DockPanel();
            Child = layout;

            var title = CreateTitle("Node Palette");
            DockPanel.SetDock(title, Dock.Top);
            layout.Children.Add(title);

            _items = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _items
            };
            layout.Children.Add(scroll);
        }

        public event Action<NodeDescriptor> NodeRequested;

        public void SetReadOnly(bool isReadOnly)
        {
            _isReadOnly = isReadOnly;
            Opacity = isReadOnly ? 0.64 : 1.0;
            ToolTip = isReadOnly ? "调试运行模式下不可新增节点。" : null;
            foreach (var button in _descriptorButtons)
            {
                button.IsEnabled = !isReadOnly;
            }
        }

        public void SetDescriptors(IEnumerable<NodeDescriptor> descriptors)
        {
            _items.Children.Clear();
            _descriptorButtons.Clear();
            string currentCategory = null;
            foreach (var descriptor in descriptors)
            {
                if (!string.Equals(currentCategory, descriptor.Category, StringComparison.OrdinalIgnoreCase))
                {
                    currentCategory = descriptor.Category;
                    _items.Children.Add(new TextBlock
                    {
                        Text = currentCategory,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                        Margin = new Thickness(0, 12, 0, 6)
                    });
                }

                var button = new Button
                {
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(9, 7, 9, 7),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = FlowDesignerControl.BrushFromRgb(248, 250, 252),
                    BorderBrush = FlowDesignerControl.BrushFromRgb(226, 232, 240),
                    IsEnabled = !_isReadOnly,
                    Tag = descriptor,
                    Content = CreatePaletteContent(descriptor)
                };
                button.Click += delegate
                {
                    if (_isReadOnly)
                    {
                        return;
                    }

                    var handler = NodeRequested;
                    if (handler != null)
                    {
                        handler(descriptor);
                    }
                };
                _descriptorButtons.Add(button);
                _items.Children.Add(button);
            }
        }

        private static UIElement CreatePaletteContent(NodeDescriptor descriptor)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = descriptor.DisplayName,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42)
            });
            panel.Children.Add(new TextBlock
            {
                Text = descriptor.NodeType,
                FontSize = 11,
                Foreground = FlowDesignerControl.BrushFromRgb(100, 116, 139),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            return panel;
        }

        private static TextBlock CreateTitle(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }
    }
}
