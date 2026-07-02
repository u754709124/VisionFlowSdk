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
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 运行调试面板显示流程事件，并暴露节点导航请求。
    public sealed class RuntimeDebugPanelControl : Border
    {
        private readonly ListBox _events;

        public RuntimeDebugPanelControl()
        {
            Margin = new Thickness(12, 0, 12, 12);
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            var layout = new DockPanel();
            Child = layout;

            var title = new TextBlock
            {
                Text = "Runtime Debug",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(title, Dock.Top);
            layout.Children.Add(title);

            _events = new ListBox
            {
                BorderThickness = new Thickness(0),
                Background = Brushes.White
            };
            _events.SelectionChanged += OnSelectionChanged;
            layout.Children.Add(_events);
        }

        public event Action<string> NodeRequested;

        public void Clear()
        {
            _events.Items.Clear();
        }

        public void AddMessage(string message)
        {
            _events.Items.Add(new ListBoxItem
            {
                Content = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message
            });
            _events.ScrollIntoView(_events.Items[_events.Items.Count - 1]);
        }

        public void AddEvent(FlowRuntimeEvent runtimeEvent)
        {
            var node = string.IsNullOrWhiteSpace(runtimeEvent.NodeId) ? "-" : runtimeEvent.NodeId;
            var text = string.Format(
                CultureInfo.InvariantCulture,
                "{0:HH:mm:ss.fff}  {1}  {2}  {3}",
                runtimeEvent.TimestampUtc.ToLocalTime(),
                runtimeEvent.EventType,
                node,
                runtimeEvent.Message ?? runtimeEvent.OutputPort ?? string.Empty);
            _events.Items.Add(new ListBoxItem
            {
                Content = text,
                Tag = runtimeEvent.NodeId
            });
            _events.ScrollIntoView(_events.Items[_events.Items.Count - 1]);
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
