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
using Vision.Flow.Nodes;
using ShapesPath = System.Windows.Shapes.Path;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // �ڵ��ؼ�����ڵ����Ŀ��Ⱦ�������ڵ�����
    public sealed class NodePaletteControl : Border
    {
        private readonly StackPanel _items;
        private readonly List<Button> _descriptorButtons;
        private NodeDescriptor _pressedDescriptor;
        private Point _dragStartPoint;
        private Button _selectedButton;
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

        public event EventHandler<NodePaletteDragEventArgs> NodeDragRequested;

        public NodeDescriptor SelectedDescriptor { get; private set; }

        public void SetReadOnly(bool isReadOnly)
        {
            _isReadOnly = isReadOnly;
            Opacity = isReadOnly ? 0.64 : 1.0;
            ToolTip = isReadOnly ? "��������ģʽ�²��������ڵ㡣" : null;
            foreach (var button in _descriptorButtons)
            {
                button.IsEnabled = !isReadOnly;
            }
        }

        public void SetDescriptors(IEnumerable<NodeDescriptor> descriptors)
        {
            _items.Children.Clear();
            _descriptorButtons.Clear();
            SelectedDescriptor = null;
            _selectedButton = null;
            _pressedDescriptor = null;
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
                ApplyDescriptorButtonVisual(button, false);
                button.Click += delegate
                {
                    if (_isReadOnly)
                    {
                        return;
                    }

                    SelectDescriptor(descriptor, button);
                };
                button.MouseDoubleClick += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (_isReadOnly)
                    {
                        return;
                    }

                    SelectDescriptor(descriptor, button);
                    RequestNode(descriptor);
                    e.Handled = true;
                };
                button.PreviewMouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    if (_isReadOnly)
                    {
                        return;
                    }

                    SelectDescriptor(descriptor, button);
                    _pressedDescriptor = descriptor;
                    _dragStartPoint = e.GetPosition(button);
                };
                button.PreviewMouseMove += delegate(object sender, MouseEventArgs e)
                {
                    if (_isReadOnly || _pressedDescriptor == null || e.LeftButton != MouseButtonState.Pressed)
                    {
                        return;
                    }

                    var point = e.GetPosition(button);
                    if (Math.Abs(point.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(point.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                    {
                        return;
                    }

                    var dragDescriptor = _pressedDescriptor;
                    _pressedDescriptor = null;
                    RequestNodeDrag(dragDescriptor, button);
                    e.Handled = true;
                };
                button.PreviewMouseLeftButtonUp += delegate
                {
                    _pressedDescriptor = null;
                };
                _descriptorButtons.Add(button);
                _items.Children.Add(button);
            }
        }

        public bool RequestNodeDrag(NodeDescriptor descriptor, UIElement dragSource)
        {
            if (_isReadOnly || descriptor == null || dragSource == null)
            {
                return false;
            }

            var button = dragSource as Button;
            if (button != null)
            {
                SelectDescriptor(descriptor, button);
            }

            var handler = NodeDragRequested;
            if (handler == null)
            {
                return false;
            }

            handler(this, new NodePaletteDragEventArgs(descriptor, dragSource));
            return true;
        }

        private void RequestNode(NodeDescriptor descriptor)
        {
            var handler = NodeRequested;
            if (handler != null)
            {
                handler(descriptor);
            }
        }

        private void SelectDescriptor(NodeDescriptor descriptor, Button button)
        {
            SelectedDescriptor = descriptor;
            if (_selectedButton != null && !object.ReferenceEquals(_selectedButton, button))
            {
                ApplyDescriptorButtonVisual(_selectedButton, false);
            }

            _selectedButton = button;
            if (_selectedButton != null)
            {
                ApplyDescriptorButtonVisual(_selectedButton, true);
            }
        }

        private static void ApplyDescriptorButtonVisual(Button button, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isSelected
                ? FlowDesignerControl.BrushFromRgb(224, 242, 254)
                : FlowDesignerControl.BrushFromRgb(248, 250, 252);
            button.BorderBrush = isSelected
                ? FlowDesignerControl.BrushFromRgb(14, 165, 233)
                : FlowDesignerControl.BrushFromRgb(226, 232, 240);
            button.BorderThickness = new Thickness(isSelected ? 1.4 : 1.0);
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
