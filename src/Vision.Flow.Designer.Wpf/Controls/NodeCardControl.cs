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
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.Theming;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 节点卡片和端口控件渲染画布节点及连线手柄。
    public sealed class NodeCardControl : Border
    {
        private readonly TextBlock _title;
        private readonly TextBlock _type;
        private readonly StackPanel _summaryRows;
        private readonly Border _cardBody;
        private readonly Border _cardShadowHost;
        private readonly System.Windows.Media.Effects.DropShadowEffect _cardShadow;
        private bool _isDisabled;
        private bool _isSelected;

        public NodeCardControl(NodeViewModel viewModel)
        {
            ViewModel = viewModel;
            Width = 220;
            MinHeight = 122;
            Background = Brushes.Transparent;
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Cursor = Cursors.SizeAll;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);
            InputPortControls = new List<PortControl>();
            OutputPortControls = new List<PortControl>();

            var outer = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            Child = outer;

            _cardShadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Opacity = 0.08,
                Color = Color.FromRgb(15, 23, 42)
            };
            _cardShadowHost = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Effect = _cardShadow,
                CacheMode = new BitmapCache(),
                IsHitTestVisible = false
            };
            _cardBody = new Border
            {
                Background = Brushes.White,
                BorderBrush = FlowDesignerControl.BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 8, 9, 8)
            };
            var cardHost = new Grid();
            cardHost.Children.Add(_cardShadowHost);
            cardHost.Children.Add(_cardBody);
            outer.Children.Add(cardHost);

            var chrome = new Grid();
            _cardBody.Child = chrome;

            var root = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(10, 0, 10, 0)
            };
            chrome.Children.Add(root);

            var ports = CreatePortRow(viewModel);
            Panel.SetZIndex(ports, 2);
            chrome.Children.Add(ports);

            var header = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var icon = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(5),
                Background = GetNodeAccentSoftBrush(viewModel.Node.Type),
                Child = FlowDesignerIcons.CreateNode(
                    viewModel.Node.Type,
                    GetNodeAccentBrush(viewModel.Node.Type),
                    14)
            };
            DockPanel.SetDock(icon, Dock.Left);
            header.Children.Add(icon);

            var text = new StackPanel
            {
                Margin = new Thickness(7, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(text);
            _title = new TextBlock
            {
                Text = viewModel.Node.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12.5,
                LineHeight = 16
            };
            _type = new TextBlock
            {
                Text = GetNodeDescription(viewModel),
                FontSize = 9.5,
                Foreground = FlowDesignerControl.BrushFromRgb(100, 116, 139),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            text.Children.Add(_title);
            text.Children.Add(_type);

            _summaryRows = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 1)
            };
            root.Children.Add(_summaryRows);
            UpdateSummary();
        }

        public NodeViewModel ViewModel { get; private set; }

        public IList<PortControl> InputPortControls { get; private set; }

        public IList<PortControl> OutputPortControls { get; private set; }

        public event EventHandler<PortConnectionEventArgs> OutputPortDragStarted;

        public event EventHandler<PortConnectionEventArgs> InputPortDragCompleted;

        public void UpdateSummary()
        {
            _title.Text = string.IsNullOrWhiteSpace(ViewModel.Node.Name) ? ViewModel.Node.Id : ViewModel.Node.Name;
            _type.Text = GetNodeDescription(ViewModel);

            _summaryRows.Children.Clear();
            foreach (var row in CreateSummaryRows())
            {
                _summaryRows.Children.Add(CreateSummaryRow(row.Key, row.Value));
            }
        }

        private static string GetNodeDescription(NodeViewModel viewModel)
        {
            return viewModel.Descriptor == null || string.IsNullOrWhiteSpace(viewModel.Descriptor.Description)
                ? viewModel.Node.Type
                : viewModel.Descriptor.Description;
        }

        private IEnumerable<KeyValuePair<string, string>> CreateSummaryRows()
        {
            var rows = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("TYPE", ShortNodeType(ViewModel.Node.Type))
            };

            var retryPolicy = ViewModel.Node.ExecutionPolicy == null
                ? null
                : ViewModel.Node.ExecutionPolicy.RetryPolicy;
            if (retryPolicy != null && retryPolicy.Enabled)
            {
                rows.Add(new KeyValuePair<string, string>(
                    "重试",
                    retryPolicy.MaxRetries.ToString(CultureInfo.InvariantCulture) + " 次 · " +
                    retryPolicy.RetryIntervalMs.ToString(CultureInfo.InvariantCulture) + " ms"));
            }

            if (rows.Count < 3 && ViewModel.Node.Settings != null)
            {
                foreach (var setting in ViewModel.Node.Settings)
                {
                    if (setting.Value == null ||
                        setting.Value.Mode != NodeSettingValueMode.Variable ||
                        setting.Value.Selector == null)
                    {
                        continue;
                    }

                    rows.Add(new KeyValuePair<string, string>(
                        setting.Key,
                        ToShortText(VariableSelectionOption.FormatSelector(setting.Value.Selector))));
                    if (rows.Count >= 3)
                    {
                        break;
                    }
                }
            }

            if (rows.Count < 2)
            {
                var ports = ViewModel.OutputPorts.Select(x => x.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Take(2).ToArray();
                rows.Add(new KeyValuePair<string, string>("OUT", ports.Length == 0 ? "default" : string.Join(", ", ports)));
            }

            return rows.Take(3);
        }

        private static UIElement CreateSummaryRow(string label, string value)
        {
            var border = new Border
            {
                MinHeight = 20,
                Margin = new Thickness(0, 0, 0, 4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = FlowDesignerControl.BrushFromRgb(243, 246, 249),
                CornerRadius = new CornerRadius(4)
            };

            var row = new DockPanel
            {
                LastChildFill = true
            };
            border.Child = row;

            var left = new TextBlock
            {
                Text = ToShortLabel(label),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(left, Dock.Left);
            row.Children.Add(left);

            var right = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
                FontSize = 10.5,
                Foreground = FlowDesignerControl.BrushFromRgb(51, 65, 85),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            row.Children.Add(right);

            return border;
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            UpdateCardChrome();
        }

        public void SetDisabled(bool isDisabled)
        {
            _isDisabled = isDisabled;
            UpdateCardChrome();
        }

        public void SetEditEnabled(bool isEditEnabled)
        {
            Cursor = isEditEnabled ? Cursors.SizeAll : Cursors.Arrow;
            foreach (var port in InputPortControls)
            {
                port.SetEditEnabled(isEditEnabled);
            }

            foreach (var port in OutputPortControls)
            {
                port.SetEditEnabled(isEditEnabled);
            }
        }

        /// <summary>
        /// 在常用的 75% 缩放附近优先使用屏幕像素提示；其它倍率保留适合几何缩放的字形度量。
        /// </summary>
        public void SetCanvasZoom(double zoom)
        {
            var useDisplayMetrics = !double.IsNaN(zoom) &&
                !double.IsInfinity(zoom) &&
                Math.Abs(zoom - 0.75) <= 0.035;
            TextOptions.SetTextFormattingMode(
                this,
                useDisplayMetrics ? TextFormattingMode.Display : TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(this, TextHintingMode.Fixed);
        }

        private void UpdateCardChrome()
        {
            var border = FlowDesignerControl.BrushFromRgb(221, 229, 239);
            var thickness = 1.0;
            var opacity = 1.0;
            var shadowOpacity = 0.08;

            if (_isDisabled)
            {
                border = FlowDesignerControl.BrushFromRgb(203, 213, 225);
                opacity = 0.58;
            }
            else if (_isSelected)
            {
                border = FlowDesignerControl.BrushFromRgb(47, 128, 237);
                thickness = 1.8;
                shadowOpacity = 0.14;
            }

            _cardBody.BorderBrush = border;
            _cardBody.BorderThickness = new Thickness(thickness);
            _cardBody.Opacity = opacity;
            _cardShadow.Opacity = shadowOpacity;
        }

        private UIElement CreatePortRow(NodeViewModel viewModel)
        {
            var row = new Grid
            {
                // 端口短条完全位于卡片描边外侧，透明命中区仍覆盖边缘附近，便于拖拽连线。
                Margin = new Thickness(-20, 0, -20, 0),
                IsHitTestVisible = true
            };

            var input = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var port in viewModel.InputPorts)
            {
                var portControl = new PortControl(port);
                InputPortControls.Add(portControl);
                portControl.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
                {
                    var handler = InputPortDragCompleted;
                    if (handler != null)
                    {
                        handler(this, new PortConnectionEventArgs(port, portControl));
                    }

                    e.Handled = true;
                };
                input.Children.Add(portControl);
            }

            row.Children.Add(input);

            var output = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            foreach (var port in viewModel.OutputPorts)
            {
                var portControl = new PortControl(port);
                OutputPortControls.Add(portControl);
                portControl.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
                {
                    var handler = OutputPortDragStarted;
                    if (handler != null)
                    {
                        handler(this, new PortConnectionEventArgs(port, portControl));
                    }

                    e.Handled = true;
                };
                output.Children.Add(portControl);
            }

            row.Children.Add(output);
            return row;
        }

        private static Brush GetNodeAccentBrush(string nodeType)
        {
            var type = nodeType ?? string.Empty;
            if (type.StartsWith(FlowNodeTypePrefixes.Camera, StringComparison.OrdinalIgnoreCase) ||
                type.IndexOf(".camera.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FlowDesignerControl.BrushFromRgb(59, 130, 246);
            }

            if (type.StartsWith(FlowNodeTypePrefixes.Join, StringComparison.OrdinalIgnoreCase))
            {
                return FlowDesignerControl.BrushFromRgb(14, 165, 233);
            }

            if (type.IndexOf("branch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("condition", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FlowDesignerControl.BrushFromRgb(6, 182, 212);
            }

            return FlowDesignerControl.BrushFromRgb(99, 102, 241);
        }

        private static Brush GetNodeAccentSoftBrush(string nodeType)
        {
            var type = nodeType ?? string.Empty;
            if (type.StartsWith(FlowNodeTypePrefixes.Camera, StringComparison.OrdinalIgnoreCase) ||
                type.IndexOf(".camera.", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FlowDesignerControl.BrushFromRgb(234, 243, 255);
            }

            if (type.IndexOf("condition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("branch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return FlowDesignerControl.BrushFromRgb(231, 249, 252);
            }

            return FlowDesignerControl.BrushFromRgb(238, 238, 255);
        }

        private static string GetNodeGlyph(string nodeType)
        {
            var type = nodeType ?? string.Empty;
            if (type.IndexOf("jwt", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "JWT";
            }

            if (type.IndexOf("http", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "GET";
            }

            if (type.StartsWith(FlowNodeTypePrefixes.Camera, StringComparison.OrdinalIgnoreCase))
            {
                return "CAM";
            }

            if (type.IndexOf("branch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                type.IndexOf("condition", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "IF";
            }

            return "VF";
        }

        private static string ShortNodeType(string nodeType)
        {
            if (string.IsNullOrWhiteSpace(nodeType))
            {
                return "node";
            }

            var text = nodeType.Trim();
            var index = text.LastIndexOf('.');
            if (index >= 0 && index < text.Length - 1)
            {
                text = text.Substring(index + 1);
            }

            return ToShortText(text);
        }

        private static string ToShortLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return "INFO";
            }

            var text = label.Trim().ToUpperInvariant();
            return text.Length <= 8 ? text : text.Substring(0, 8);
        }

        private static string ToShortText(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(text) || text.StartsWith("System.", StringComparison.Ordinal))
            {
                text = value is System.Collections.IEnumerable && !(value is string) ? "list" : "object";
            }

            return text.Length <= 22 ? text : text.Substring(0, 19) + "...";
        }
    }

}
