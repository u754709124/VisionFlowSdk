using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Designer.Wpf.Theming;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 节点库支持描述符搜索、分类折叠、双击创建与拖拽创建。
    /// </summary>
    public sealed class NodePaletteControl : Border
    {
        private readonly StackPanel _items;
        private readonly TextBox _searchBox;
        private readonly TextBlock _searchPlaceholder;
        private readonly List<Button> _descriptorButtons;
        private readonly List<NodeDescriptor> _descriptors;
        private readonly Dictionary<string, bool> _categoryExpansionState;
        private NodeDescriptor _pressedDescriptor;
        private Point _dragStartPoint;
        private Button _selectedButton;
        private bool _isReadOnly;
        private bool _isRendering;

        public NodePaletteControl()
        {
            _descriptorButtons = new List<Button>();
            _descriptors = new List<NodeDescriptor>();
            _categoryExpansionState = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Margin = new Thickness(0);
            Padding = new Thickness(0);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(221, 229, 239);
            BorderThickness = new Thickness(0, 0, 1, 0);
            CornerRadius = new CornerRadius(0);
            FlowDesignerTheme.ApplyTo(this);

            var layout = new DockPanel();
            Child = layout;

            var header = new StackPanel
            {
                Margin = new Thickness(14, 13, 14, 10)
            };
            DockPanel.SetDock(header, Dock.Top);
            layout.Children.Add(header);

            header.Children.Add(CreateTitle("节点库"));
            var searchFrame = new Grid
            {
                Margin = new Thickness(0, 9, 0, 0)
            };
            _searchBox = new TextBox
            {
                Tag = "NodePaletteSearch",
                MinHeight = 38,
                Padding = new Thickness(34, 8, 10, 8),
                Background = FlowDesignerControl.BrushFromRgb(248, 250, 252),
                BorderBrush = FlowDesignerControl.BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(1)
            };
            _searchBox.TextChanged += delegate
            {
                _searchPlaceholder.Visibility = string.IsNullOrWhiteSpace(_searchBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                RenderDescriptors();
            };
            searchFrame.Children.Add(_searchBox);
            var searchIcon = FlowDesignerIcons.Create(
                "search",
                FlowDesignerControl.BrushFromRgb(122, 135, 154),
                15);
            searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
            searchIcon.VerticalAlignment = VerticalAlignment.Center;
            searchIcon.Margin = new Thickness(11, 0, 0, 0);
            searchIcon.IsHitTestVisible = false;
            searchFrame.Children.Add(searchIcon);
            _searchPlaceholder = new TextBlock
            {
                Text = "搜索节点",
                Foreground = FlowDesignerControl.BrushFromRgb(148, 160, 176),
                Margin = new Thickness(34, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchFrame.Children.Add(_searchPlaceholder);
            header.Children.Add(searchFrame);

            _items = new StackPanel
            {
                Margin = new Thickness(10, 0, 10, 12)
            };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _items
            };
            layout.Children.Add(scroll);
        }

        public event Action<NodeDescriptor> NodeRequested;

        public event EventHandler<NodePaletteDragEventArgs> NodeDragRequested;

        public NodeDescriptor SelectedDescriptor { get; private set; }

        public string SearchText
        {
            get { return _searchBox.Text; }
            set { _searchBox.Text = value ?? string.Empty; }
        }

        public void SetReadOnly(bool isReadOnly)
        {
            _isReadOnly = isReadOnly;
            ToolTip = isReadOnly ? "当前为只读模式，不能新增节点。" : null;
            foreach (var button in _descriptorButtons)
            {
                button.IsEnabled = !isReadOnly;
            }
        }

        public void SetDescriptors(IEnumerable<NodeDescriptor> descriptors)
        {
            _descriptors.Clear();
            if (descriptors != null)
            {
                _descriptors.AddRange(descriptors.Where(x => x != null));
            }

            foreach (var category in _descriptors
                .Select(x => NormalizeCategory(x.Category))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!_categoryExpansionState.ContainsKey(category))
                {
                    _categoryExpansionState[category] = true;
                }
            }

            SelectedDescriptor = null;
            _selectedButton = null;
            _pressedDescriptor = null;
            RenderDescriptors();
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

        private void RenderDescriptors()
        {
            if (_isRendering)
            {
                return;
            }

            _isRendering = true;
            try
            {
                _items.Children.Clear();
                _descriptorButtons.Clear();
                _selectedButton = null;
                var search = (_searchBox.Text ?? string.Empty).Trim();
                var matches = _descriptors
                    .Where(x => MatchesSearch(x, search))
                    .GroupBy(x => NormalizeCategory(x.Category), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                if (matches.Count == 0)
                {
                    _items.Children.Add(new TextBlock
                    {
                        Text = "未找到匹配节点",
                        Foreground = FlowDesignerControl.BrushFromRgb(122, 135, 154),
                        Margin = new Thickness(8, 18, 8, 0),
                        TextAlignment = TextAlignment.Center
                    });
                    return;
                }

                foreach (var group in matches)
                {
                    var category = group.Key;
                    bool savedExpanded;
                    if (!_categoryExpansionState.TryGetValue(category, out savedExpanded))
                    {
                        savedExpanded = true;
                    }

                    var expander = new Expander
                    {
                        Header = CreateCategoryHeader(category, group.Count()),
                        IsExpanded = !string.IsNullOrWhiteSpace(search) || savedExpanded,
                        Margin = new Thickness(0, 3, 0, 2),
                        Tag = "NodePaletteCategory:" + category
                    };
                    expander.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.ExpanderStyleKey);
                    expander.Expanded += delegate
                    {
                        if (string.IsNullOrWhiteSpace(_searchBox.Text))
                        {
                            _categoryExpansionState[category] = true;
                        }
                    };
                    expander.Collapsed += delegate
                    {
                        if (string.IsNullOrWhiteSpace(_searchBox.Text))
                        {
                            _categoryExpansionState[category] = false;
                        }
                    };

                    var categoryItems = new StackPanel
                    {
                        Margin = new Thickness(0, 4, 0, 3)
                    };
                    foreach (var descriptor in group
                        .OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                    {
                        categoryItems.Children.Add(CreateDescriptorButton(descriptor));
                    }

                    expander.Content = categoryItems;
                    _items.Children.Add(expander);
                }
            }
            finally
            {
                _isRendering = false;
            }
        }

        private Button CreateDescriptorButton(NodeDescriptor descriptor)
        {
            var button = new Button
            {
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(9, 8, 9, 8),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = Brushes.White,
                BorderBrush = FlowDesignerControl.BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                IsEnabled = !_isReadOnly,
                Tag = descriptor,
                Content = CreatePaletteContent(descriptor),
                Cursor = Cursors.Hand
            };
            ApplyDescriptorButtonVisual(button, SelectedDescriptor != null &&
                string.Equals(SelectedDescriptor.NodeType, descriptor.NodeType, StringComparison.OrdinalIgnoreCase));
            button.Click += delegate
            {
                if (!_isReadOnly)
                {
                    SelectDescriptor(descriptor, button);
                }
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
            button.PreviewMouseLeftButtonUp += delegate { _pressedDescriptor = null; };
            _descriptorButtons.Add(button);
            if (SelectedDescriptor != null &&
                string.Equals(SelectedDescriptor.NodeType, descriptor.NodeType, StringComparison.OrdinalIgnoreCase))
            {
                _selectedButton = button;
            }

            return button;
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

        private static bool MatchesSearch(NodeDescriptor descriptor, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return Contains(descriptor.DisplayName, search) ||
                Contains(descriptor.Description, search) ||
                Contains(descriptor.NodeType, search) ||
                Contains(descriptor.Category, search);
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? "其他" : category.Trim();
        }

        private static void ApplyDescriptorButtonVisual(Button button, bool isSelected)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isSelected
                ? FlowDesignerControl.BrushFromRgb(234, 243, 255)
                : Brushes.White;
            button.BorderBrush = isSelected
                ? FlowDesignerControl.BrushFromRgb(47, 128, 237)
                : FlowDesignerControl.BrushFromRgb(226, 232, 240);
            button.BorderThickness = new Thickness(isSelected ? 1.5 : 1.0);
        }

        private static UIElement CreatePaletteContent(NodeDescriptor descriptor)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconFrame = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = FlowDesignerControl.BrushFromRgb(234, 248, 242),
                Child = FlowDesignerIcons.CreateNode(
                    descriptor.NodeType,
                    FlowDesignerControl.BrushFromRgb(16, 163, 114),
                    15)
            };
            Grid.SetColumn(iconFrame, 0);
            row.Children.Add(iconFrame);

            var panel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(new TextBlock
            {
                Text = descriptor.DisplayName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = FlowDesignerControl.BrushFromRgb(36, 50, 71),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            panel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(descriptor.Description) ? descriptor.NodeType : descriptor.Description,
                FontSize = 10.5,
                Foreground = FlowDesignerControl.BrushFromRgb(122, 135, 154),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(panel, 1);
            row.Children.Add(panel);
            return row;
        }

        private static UIElement CreateCategoryHeader(string category, int count)
        {
            var row = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(3, 5, 2, 5)
            };
            var countText = new TextBlock
            {
                Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Foreground = FlowDesignerControl.BrushFromRgb(148, 160, 176),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(countText, Dock.Right);
            row.Children.Add(countText);
            row.Children.Add(new TextBlock
            {
                Text = category,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5,
                Foreground = FlowDesignerControl.BrushFromRgb(75, 91, 112),
                VerticalAlignment = VerticalAlignment.Center
            });
            return row;
        }

        private static TextBlock CreateTitle(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(36, 50, 71)
            };
        }
    }
}
