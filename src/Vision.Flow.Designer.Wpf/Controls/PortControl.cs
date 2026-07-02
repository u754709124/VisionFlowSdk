using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 端口控件只负责端口手柄的命中区域和锚点计算。
    public sealed class PortControl : Border
    {
        private readonly Border _tab;
        private readonly Brush _normalFill;
        private readonly Brush _hoverFill;
        private readonly Cursor _editCursor;

        public PortControl(PortViewModel port)
        {
            Port = port;
            var isInput = port != null && port.Direction == FlowPortDirection.Input;
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
            ToolTip = (isInput ? "Input: " : "Output: ") + (port == null ? string.Empty : port.Name + " (" + FlowEnumConverter.ToWireValue(port.DataType) + ")");
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
}
