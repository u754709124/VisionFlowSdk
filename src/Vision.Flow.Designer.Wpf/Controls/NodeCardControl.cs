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
    // 节点卡片和端口控件渲染画布节点及连线手柄。
    public sealed class NodeCardControl : Border
    {
        private readonly TextBlock _title;
        private readonly TextBlock _type;
        private readonly StackPanel _summaryRows;
        private readonly Border _cardBody;
        private readonly Border _runtimeSummary;
        private readonly TextBlock _runtimeSummaryText;
        private readonly Border _stateChip;
        private readonly TextBlock _stateText;
        private readonly System.Windows.Media.Effects.DropShadowEffect _cardShadow;
        private bool _isDisabled;
        private bool _isSelected;
        private bool _hasRuntimeState;
        private NodeRuntimeState _runtimeState;

        public NodeCardControl(NodeViewModel viewModel)
        {
            ViewModel = viewModel;
            Width = 190;
            MinHeight = 118;
            Background = Brushes.Transparent;
            BorderBrush = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Padding = new Thickness(0);
            Cursor = Cursors.SizeAll;
            InputPortControls = new List<PortControl>();
            OutputPortControls = new List<PortControl>();

            var outer = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            Child = outer;

            _runtimeSummaryText = new TextBlock
            {
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            _runtimeSummary = new Border
            {
                Height = 24,
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(8, 0, 8, 0),
                CornerRadius = new CornerRadius(5),
                Background = FlowDesignerControl.BrushFromRgb(241, 245, 249),
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225),
                BorderThickness = new Thickness(1),
                Child = _runtimeSummaryText,
                Visibility = Visibility.Collapsed
            };
            outer.Children.Add(_runtimeSummary);

            _cardShadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Opacity = 0.08,
                Color = Color.FromRgb(15, 23, 42)
            };
            _cardBody = new Border
            {
                Background = Brushes.White,
                BorderBrush = FlowDesignerControl.BrushFromRgb(52, 211, 153),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(9, 8, 9, 8),
                Effect = _cardShadow
            };
            outer.Children.Add(_cardBody);

            var chrome = new Grid();
            _cardBody.Child = chrome;

            var root = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(12, 0, 12, 0)
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
                Background = GetNodeAccentBrush(viewModel.Node.Type),
                Child = new TextBlock
                {
                    Text = GetNodeGlyph(viewModel.Node.Type),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                }
            };
            DockPanel.SetDock(icon, Dock.Left);
            header.Children.Add(icon);

            _stateChip = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(8, 6, 0, 0),
                Background = FlowDesignerControl.BrushFromRgb(16, 185, 129),
                ToolTip = "Ready",
                Child = _stateText = new TextBlock
                {
                    Text = string.Empty,
                    FontSize = 1
                }
            };
            DockPanel.SetDock(_stateChip, Dock.Right);
            header.Children.Add(_stateChip);

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
                Text = viewModel.Node.Type,
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
            _type.Text = ViewModel.Node.Type;

            _summaryRows.Children.Clear();
            foreach (var row in CreateSummaryRows())
            {
                _summaryRows.Children.Add(CreateSummaryRow(row.Key, row.Value));
            }
        }

        private IEnumerable<KeyValuePair<string, string>> CreateSummaryRows()
        {
            var rows = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("TYPE", ShortNodeType(ViewModel.Node.Type))
            };

            if (ViewModel.Node.InputBindings != null)
            {
                foreach (var binding in ViewModel.Node.InputBindings)
                {
                    if (binding.Value == null || string.IsNullOrWhiteSpace(binding.Value.Expression))
                    {
                        continue;
                    }

                    rows.Add(new KeyValuePair<string, string>(binding.Key, ToShortText(binding.Value.Expression)));
                    break;
                }
            }

            if (rows.Count < 3 && ViewModel.Node.Settings != null)
            {
                foreach (var setting in ViewModel.Node.Settings)
                {
                    if (setting.Value == null)
                    {
                        continue;
                    }

                    rows.Add(new KeyValuePair<string, string>(setting.Key, ToShortText(setting.Value)));
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
            UpdateRuntimeVisual(null);
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

        public void SetRuntimeState(NodeRuntimeState state)
        {
            SetRuntimeState(state, null, null);
        }

        public void SetRuntimeState(NodeRuntimeState state, TimeSpan? elapsed, string message)
        {
            _hasRuntimeState = true;
            _runtimeState = state;
            ToolTip = string.IsNullOrWhiteSpace(message) ? null : message;
            UpdateRuntimeVisual(elapsed);
            UpdateCardChrome();
            if (state == NodeRuntimeState.Failed || state == NodeRuntimeState.Timeout)
            {
                _runtimeSummary.ToolTip = string.IsNullOrWhiteSpace(message) ? _runtimeSummaryText.Text : message;
            }
        }

        public void ClearRuntimeState()
        {
            _hasRuntimeState = false;
            _runtimeState = NodeRuntimeState.Waiting;
            ToolTip = null;
            _runtimeSummary.ToolTip = null;
            _runtimeSummary.Visibility = Visibility.Collapsed;
            UpdateRuntimeVisual(null);
            UpdateCardChrome();
        }

        private void UpdateRuntimeVisual(TimeSpan? elapsed)
        {
            if (!_hasRuntimeState)
            {
                _stateChip.Background = _isDisabled
                    ? FlowDesignerControl.BrushFromRgb(226, 232, 240)
                    : FlowDesignerControl.BrushFromRgb(16, 185, 129);
                _stateChip.ToolTip = _isDisabled ? "Disabled" : "Ready";
                _stateText.Text = string.Empty;
                return;
            }

            _runtimeSummary.Visibility = Visibility.Visible;
            if (_isDisabled && _runtimeState == NodeRuntimeState.Waiting)
            {
                ApplyRuntimeSummary("禁用", FlowDesignerControl.BrushFromRgb(241, 245, 249), FlowDesignerControl.BrushFromRgb(203, 213, 225), FlowDesignerControl.BrushFromRgb(100, 116, 139));
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(226, 232, 240);
                _stateChip.ToolTip = "Disabled";
                _stateText.Text = string.Empty;
                return;
            }

            if (_runtimeState == NodeRuntimeState.Running)
            {
                ApplyRuntimeSummary("运行中", FlowDesignerControl.BrushFromRgb(254, 243, 199), FlowDesignerControl.BrushFromRgb(245, 158, 11), FlowDesignerControl.BrushFromRgb(146, 64, 14));
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(245, 158, 11);
                _stateChip.ToolTip = "Running";
                _stateText.Text = string.Empty;
                return;
            }

            if (_runtimeState == NodeRuntimeState.Completed)
            {
                ApplyRuntimeSummary("成功" + FormatElapsedSuffix(elapsed), FlowDesignerControl.BrushFromRgb(220, 252, 231), FlowDesignerControl.BrushFromRgb(34, 197, 94), FlowDesignerControl.BrushFromRgb(21, 128, 61));
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(16, 185, 129);
                _stateChip.ToolTip = elapsed.HasValue ? "Done " + FormatElapsed(elapsed.Value) : "Done";
                _stateText.Text = string.Empty;
                return;
            }

            if (_runtimeState == NodeRuntimeState.Failed)
            {
                ApplyRuntimeSummary("失败" + FormatElapsedSuffix(elapsed) + FormatMessageSuffix(ToolTip), FlowDesignerControl.BrushFromRgb(254, 226, 226), FlowDesignerControl.BrushFromRgb(239, 68, 68), FlowDesignerControl.BrushFromRgb(153, 27, 27));
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(239, 68, 68);
                _stateChip.ToolTip = elapsed.HasValue ? "Failed " + FormatElapsed(elapsed.Value) : "Failed";
                _stateText.Text = string.Empty;
                return;
            }

            if (_runtimeState == NodeRuntimeState.Timeout)
            {
                ApplyRuntimeSummary("超时" + FormatElapsedSuffix(elapsed) + FormatMessageSuffix(ToolTip), FlowDesignerControl.BrushFromRgb(255, 237, 213), FlowDesignerControl.BrushFromRgb(249, 115, 22), FlowDesignerControl.BrushFromRgb(154, 52, 18));
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(249, 115, 22);
                _stateChip.ToolTip = elapsed.HasValue ? "Timeout " + FormatElapsed(elapsed.Value) : "Timeout";
                _stateText.Text = string.Empty;
                return;
            }

            ApplyRuntimeSummary("未运行", FlowDesignerControl.BrushFromRgb(241, 245, 249), FlowDesignerControl.BrushFromRgb(203, 213, 225), FlowDesignerControl.BrushFromRgb(100, 116, 139));
            _stateChip.Background = FlowDesignerControl.BrushFromRgb(148, 163, 184);
            _stateChip.ToolTip = "Waiting";
            _stateText.Text = string.Empty;
        }

        private void UpdateCardChrome()
        {
            var border = FlowDesignerControl.BrushFromRgb(52, 211, 153);
            var thickness = 1.0;
            var opacity = 1.0;
            var shadowOpacity = 0.08;

            if (_isDisabled)
            {
                border = FlowDesignerControl.BrushFromRgb(203, 213, 225);
                opacity = 0.58;
            }
            else if (_hasRuntimeState && _runtimeState == NodeRuntimeState.Waiting)
            {
                border = FlowDesignerControl.BrushFromRgb(203, 213, 225);
                opacity = 0.68;
            }

            if (_hasRuntimeState && _runtimeState == NodeRuntimeState.Running)
            {
                border = FlowDesignerControl.BrushFromRgb(245, 158, 11);
                thickness = 2.2;
                opacity = 1.0;
                shadowOpacity = 0.18;
            }
            else if (_hasRuntimeState && _runtimeState == NodeRuntimeState.Completed)
            {
                border = FlowDesignerControl.BrushFromRgb(34, 197, 94);
                thickness = 1.6;
                opacity = 1.0;
            }
            else if (_hasRuntimeState && _runtimeState == NodeRuntimeState.Failed)
            {
                border = FlowDesignerControl.BrushFromRgb(239, 68, 68);
                thickness = 1.8;
                opacity = 1.0;
            }
            else if (_hasRuntimeState && _runtimeState == NodeRuntimeState.Timeout)
            {
                border = FlowDesignerControl.BrushFromRgb(249, 115, 22);
                thickness = 1.8;
                opacity = 1.0;
            }
            else if (_isSelected)
            {
                border = FlowDesignerControl.BrushFromRgb(16, 185, 129);
                thickness = 1.6;
            }

            _cardBody.BorderBrush = border;
            _cardBody.BorderThickness = new Thickness(thickness);
            _cardBody.Opacity = opacity;
            _cardShadow.Opacity = shadowOpacity;
        }

        private void ApplyRuntimeSummary(string text, Brush background, Brush border, Brush foreground)
        {
            _runtimeSummaryText.Text = text;
            _runtimeSummaryText.Foreground = foreground;
            _runtimeSummary.Background = background;
            _runtimeSummary.BorderBrush = border;
            _runtimeSummary.ToolTip = text;
        }

        private UIElement CreatePortRow(NodeViewModel viewModel)
        {
            var row = new Grid
            {
                Margin = new Thickness(-16, 0, -16, 0),
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

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalMilliseconds < 1000
                ? Math.Max(1, (int)elapsed.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + "ms"
                : elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        private static string FormatElapsedSuffix(TimeSpan? elapsed)
        {
            return elapsed.HasValue ? " · " + FormatElapsed(elapsed.Value) : string.Empty;
        }

        private static string FormatMessageSuffix(object message)
        {
            var text = Convert.ToString(message, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? string.Empty : " · " + ToShortText(text);
        }

        private static Brush GetNodeAccentBrush(string nodeType)
        {
            var type = nodeType ?? string.Empty;
            if (type.StartsWith(FlowNodeTypePrefixes.Camera, StringComparison.OrdinalIgnoreCase))
            {
                return FlowDesignerControl.BrushFromRgb(59, 130, 246);
            }

            if (type.StartsWith(FlowNodeTypePrefixes.Light, StringComparison.OrdinalIgnoreCase))
            {
                return FlowDesignerControl.BrushFromRgb(245, 158, 11);
            }

            if (type.StartsWith(FlowNodeTypePrefixes.Database, StringComparison.OrdinalIgnoreCase))
            {
                return FlowDesignerControl.BrushFromRgb(20, 184, 166);
            }

            if (type.StartsWith(FlowNodeTypePrefixes.Join, StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith(FlowNodeTypePrefixes.Group, StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith(FlowNodeTypePrefixes.Scan, StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith(FlowNodeTypePrefixes.Fusion, StringComparison.OrdinalIgnoreCase))
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

            if (type.StartsWith(FlowNodeTypePrefixes.Database, StringComparison.OrdinalIgnoreCase))
            {
                return "DB";
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

    public sealed class PortControl : Border
    {
        private readonly Border _tab;
        private readonly Brush _normalFill;
        private readonly Brush _hoverFill;
        private readonly Cursor _editCursor;

        public PortControl(PortViewModel port)
        {
            Port = port;
            var isInput = port != null && string.Equals(port.Direction, "Input", StringComparison.OrdinalIgnoreCase);
            _normalFill = FlowDesignerControl.BrushFromRgb(59, 130, 246);
            _hoverFill = FlowDesignerControl.BrushFromRgb(37, 99, 235);
            _editCursor = isInput ? Cursors.Cross : Cursors.Hand;

            Width = 14;
            Height = 22;
            CornerRadius = new CornerRadius(0);
            Margin = new Thickness(0, 4, 0, 4);
            Padding = new Thickness(0);
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Cursor = _editCursor;
            ToolTip = (isInput ? "Input: " : "Output: ") + (port == null ? string.Empty : port.Name + " (" + port.DataType + ")");
            SnapsToDevicePixels = true;

            var grid = new Grid();
            _tab = new Border
            {
                Width = 5,
                Height = 18,
                Background = _normalFill,
                CornerRadius = new CornerRadius(2.5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(_tab);
            Child = grid;

            MouseEnter += delegate { SetHover(true); };
            MouseLeave += delegate { SetHover(false); };
        }

        public PortViewModel Port { get; private set; }

        public Point GetAnchorPoint(UIElement relativeTo)
        {
            var width = _tab.ActualWidth > 0 ? _tab.ActualWidth : _tab.Width;
            var height = _tab.ActualHeight > 0 ? _tab.ActualHeight : _tab.Height;
            if (double.IsNaN(width) || width <= 0)
            {
                width = 5;
            }

            if (double.IsNaN(height) || height <= 0)
            {
                height = 18;
            }

            return _tab.TranslatePoint(new Point(width / 2.0, height / 2.0), relativeTo);
        }

        public void SetEditEnabled(bool isEditEnabled)
        {
            IsHitTestVisible = isEditEnabled;
            Cursor = isEditEnabled ? _editCursor : Cursors.Arrow;
            Opacity = isEditEnabled ? 1.0 : 0.72;
        }

        private void SetHover(bool isHover)
        {
            _tab.Background = isHover ? _hoverFill : _normalFill;
            _tab.Width = isHover ? 6 : 5;
        }
    }

    public sealed class PortConnectionEventArgs : EventArgs
    {
        public PortConnectionEventArgs(PortViewModel port, PortControl portControl)
        {
            Port = port;
            PortControl = portControl;
        }

        public PortViewModel Port { get; private set; }

        public PortControl PortControl { get; private set; }
    }
}
