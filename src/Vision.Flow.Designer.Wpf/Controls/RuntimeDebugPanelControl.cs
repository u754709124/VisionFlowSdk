using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Designer.Wpf.Theming;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 可折叠的运行调试抽屉。收起时只保留状态栏，展开后显示完整事件列表。
    /// </summary>
    public sealed class RuntimeDebugPanelControl : Border
    {
        private readonly ListBox _events;
        private readonly Grid _content;
        private readonly FrameworkElement _chevron;

        public RuntimeDebugPanelControl()
        {
            Margin = new Thickness(0);
            Padding = new Thickness(0);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(221, 229, 239);
            BorderThickness = new Thickness(0, 1, 0, 0);

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Child = layout;

            var header = new Button
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Cursor = Cursors.Hand,
                Tag = "RuntimeDebugDrawerToggle"
            };
            var headerContent = new DockPanel();
            _chevron = FlowDesignerIcons.Create("chevron", FlowDesignerControl.BrushFromRgb(100, 116, 139), 13);
            _chevron.RenderTransformOrigin = new Point(0.5, 0.5);
            _chevron.RenderTransform = new RotateTransform(-90);
            DockPanel.SetDock(_chevron, Dock.Right);
            headerContent.Children.Add(_chevron);
            headerContent.Children.Add(new TextBlock
            {
                Text = "运行调试",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(36, 50, 71),
                VerticalAlignment = VerticalAlignment.Center
            });
            header.Content = headerContent;
            header.Click += delegate
            {
                SetExpanded(!IsExpanded);
                var handler = ExpansionChanged;
                if (handler != null)
                {
                    handler(IsExpanded);
                }
            };
            Grid.SetRow(header, 0);
            layout.Children.Add(header);

            _content = new Grid
            {
                Background = FlowDesignerControl.BrushFromRgb(248, 250, 252),
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_content, 1);
            layout.Children.Add(_content);

            _events = new ListBox
            {
                Margin = new Thickness(12, 8, 12, 10),
                BorderBrush = FlowDesignerControl.BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Padding = new Thickness(4)
            };
            _events.SelectionChanged += OnSelectionChanged;
            _content.Children.Add(_events);
        }

        public event Action<string> NodeRequested;

        public event Action<bool> ExpansionChanged;

        public bool IsExpanded { get; private set; }

        public void SetExpanded(bool isExpanded)
        {
            IsExpanded = isExpanded;
            _content.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
            var rotate = _chevron.RenderTransform as RotateTransform;
            if (rotate != null)
            {
                rotate.Angle = isExpanded ? 0 : -90;
            }
        }

        public void Clear()
        {
            _events.Items.Clear();
        }

        public void AddMessage(string message)
        {
            _events.Items.Add(CreateEventItem(
                DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                message,
                null));
            ScrollToLast();
        }

        public void AddEvent(FlowRuntimeEvent runtimeEvent)
        {
            var node = string.IsNullOrWhiteSpace(runtimeEvent.NodeId) ? "-" : runtimeEvent.NodeId;
            var text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  {1}  {2}",
                runtimeEvent.EventType,
                node,
                runtimeEvent.Message ?? runtimeEvent.OutputPort ?? string.Empty);
            _events.Items.Add(CreateEventItem(
                runtimeEvent.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                text,
                runtimeEvent.NodeId));
            ScrollToLast();
        }

        private static ListBoxItem CreateEventItem(string timestamp, string message, string nodeId)
        {
            var row = new DockPanel { LastChildFill = true };
            var time = new TextBlock
            {
                Text = timestamp,
                Width = 92,
                Foreground = FlowDesignerControl.BrushFromRgb(122, 135, 154),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(time, Dock.Left);
            row.Children.Add(time);
            row.Children.Add(new TextBlock
            {
                Text = message ?? string.Empty,
                Foreground = FlowDesignerControl.BrushFromRgb(51, 65, 85),
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            return new ListBoxItem
            {
                Content = row,
                Tag = nodeId,
                MinHeight = 27,
                Padding = new Thickness(6, 2, 6, 2)
            };
        }

        private void ScrollToLast()
        {
            if (_events.Items.Count > 0)
            {
                _events.ScrollIntoView(_events.Items[_events.Items.Count - 1]);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _events.SelectedItem as ListBoxItem;
            var nodeId = item == null ? null : item.Tag as string;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var handler = NodeRequested;
            if (handler != null)
            {
                handler(nodeId);
            }
        }
    }
}
