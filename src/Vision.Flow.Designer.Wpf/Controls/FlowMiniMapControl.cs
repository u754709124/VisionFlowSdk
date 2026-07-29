using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 以整个逻辑画布为坐标系显示节点布局、连线和当前视野，并允许拖动视野框导航。
    /// </summary>
    public sealed class FlowMiniMapControl : FrameworkElement
    {
        private const double OuterRadius = 8;
        private const double ContentPadding = 9;
        private const double NodeWidth = 220;
        private const double NodeHeight = 150;

        private FlowDesignDocument _document;
        private double _canvasWidth;
        private double _canvasHeight;
        private double _zoom;
        private double _horizontalOffset;
        private double _verticalOffset;
        private double _viewportWidth;
        private double _viewportHeight;
        private bool _isDragging;
        private Point _dragOffset;

        public FlowMiniMapControl()
        {
            Cursor = Cursors.SizeAll;
            ToolTip = "画布缩略图：拖动蓝色视野框可快速导航";
            SnapsToDevicePixels = true;
            Focusable = false;
        }

        public event Action<Point> ViewportRequested;

        public void UpdateView(
            FlowDesignDocument document,
            double canvasWidth,
            double canvasHeight,
            double zoom,
            double horizontalOffset,
            double verticalOffset,
            double viewportWidth,
            double viewportHeight)
        {
            _document = document;
            _canvasWidth = NormalizeExtent(canvasWidth);
            _canvasHeight = NormalizeExtent(canvasHeight);
            _zoom = IsFinite(zoom) && zoom > 0 ? zoom : 1.0;
            _horizontalOffset = Math.Max(0, horizontalOffset);
            _verticalOffset = Math.Max(0, verticalOffset);
            _viewportWidth = Math.Max(0, viewportWidth);
            _viewportHeight = Math.Max(0, viewportHeight);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            var bounds = new Rect(0.5, 0.5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1));
            drawingContext.DrawRoundedRectangle(
                Brushes.White,
                new Pen(FlowDesignerControl.BrushFromRgb(221, 229, 239), 1),
                bounds,
                OuterRadius,
                OuterRadius);

            var content = GetContentBounds();
            drawingContext.PushClip(new RectangleGeometry(content, 5, 5));
            drawingContext.DrawRectangle(FlowDesignerControl.BrushFromRgb(248, 250, 252), null, content);

            if (_canvasWidth > 0 && _canvasHeight > 0)
            {
                var canvasBounds = GetCanvasBounds(content);
                DrawEdges(drawingContext, canvasBounds);
                DrawNodes(drawingContext, canvasBounds);
                DrawViewport(drawingContext, canvasBounds);
            }

            drawingContext.Pop();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            var logicalPoint = ToLogicalPoint(e.GetPosition(this));
            var viewport = GetLogicalViewport();
            _dragOffset = viewport.Contains(logicalPoint)
                ? new Point(logicalPoint.X - viewport.X, logicalPoint.Y - viewport.Y)
                : new Point(viewport.Width / 2.0, viewport.Height / 2.0);
            _isDragging = true;
            CaptureMouse();
            RequestViewport(logicalPoint);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            RequestViewport(ToLogicalPoint(e.GetPosition(this)));
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        private void DrawEdges(DrawingContext drawingContext, Rect content)
        {
            if (_document == null || _document.Runtime == null || _document.View == null ||
                _document.Runtime.Edges == null || _document.View.Nodes == null)
            {
                return;
            }

            var pen = new Pen(FlowDesignerControl.BrushFromRgb(203, 213, 225), 1);
            foreach (var edge in _document.Runtime.Edges)
            {
                NodeViewState from;
                NodeViewState to;
                if (!_document.View.Nodes.TryGetValue(edge.FromNodeId, out from) ||
                    !_document.View.Nodes.TryGetValue(edge.ToNodeId, out to) ||
                    from == null || to == null)
                {
                    continue;
                }

                drawingContext.DrawLine(
                    pen,
                    ToMiniMapPoint(new Point(from.X + NodeWidth, from.Y + NodeHeight / 2.0), content),
                    ToMiniMapPoint(new Point(to.X, to.Y + NodeHeight / 2.0), content));
            }
        }

        private void DrawNodes(DrawingContext drawingContext, Rect content)
        {
            if (_document == null || _document.Runtime == null || _document.View == null ||
                _document.Runtime.Nodes == null || _document.View.Nodes == null)
            {
                return;
            }

            var fill = FlowDesignerControl.BrushFromRgb(255, 255, 255);
            var pen = new Pen(FlowDesignerControl.BrushFromRgb(100, 116, 139), 1);
            foreach (var nodeDefinition in _document.Runtime.Nodes)
            {
                NodeViewState state;
                if (nodeDefinition == null ||
                    !_document.View.Nodes.TryGetValue(nodeDefinition.Id, out state) ||
                    state == null)
                {
                    continue;
                }

                var topLeft = ToMiniMapPoint(new Point(state.X, state.Y), content);
                var bottomRight = ToMiniMapPoint(new Point(state.X + NodeWidth, state.Y + NodeHeight), content);
                var node = new Rect(
                    topLeft,
                    new Size(Math.Max(3, bottomRight.X - topLeft.X), Math.Max(2, bottomRight.Y - topLeft.Y)));
                drawingContext.DrawRoundedRectangle(fill, pen, node, 1.5, 1.5);
            }
        }

        private void DrawViewport(DrawingContext drawingContext, Rect content)
        {
            var viewport = GetLogicalViewport();
            var topLeft = ToMiniMapPoint(viewport.TopLeft, content);
            var bottomRight = ToMiniMapPoint(viewport.BottomRight, content);
            drawingContext.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(28, 47, 128, 237)),
                new Pen(FlowDesignerControl.BrushFromRgb(47, 128, 237), 1.5),
                new Rect(topLeft, bottomRight));
        }

        private void RequestViewport(Point logicalPoint)
        {
            var viewport = GetLogicalViewport();
            var target = CalculateViewportOrigin(
                logicalPoint,
                _dragOffset,
                _canvasWidth,
                _canvasHeight,
                viewport.Width,
                viewport.Height);
            var handler = ViewportRequested;
            if (handler != null)
            {
                handler(target);
            }
        }

        private static Point CalculateViewportOrigin(
            Point logicalPoint,
            Point dragOffset,
            double canvasWidth,
            double canvasHeight,
            double viewportWidth,
            double viewportHeight)
        {
            var maxX = Math.Max(0, canvasWidth - viewportWidth);
            var maxY = Math.Max(0, canvasHeight - viewportHeight);
            return new Point(
                Math.Max(0, Math.Min(maxX, logicalPoint.X - dragOffset.X)),
                Math.Max(0, Math.Min(maxY, logicalPoint.Y - dragOffset.Y)));
        }

        private Rect GetLogicalViewport()
        {
            var width = Math.Min(_canvasWidth, _viewportWidth / _zoom);
            var height = Math.Min(_canvasHeight, _viewportHeight / _zoom);
            var x = Math.Max(0, Math.Min(Math.Max(0, _canvasWidth - width), _horizontalOffset / _zoom));
            var y = Math.Max(0, Math.Min(Math.Max(0, _canvasHeight - height), _verticalOffset / _zoom));
            return new Rect(x, y, Math.Max(0, width), Math.Max(0, height));
        }

        private Point ToLogicalPoint(Point point)
        {
            var canvasBounds = GetCanvasBounds(GetContentBounds());
            var x = Math.Max(canvasBounds.Left, Math.Min(canvasBounds.Right, point.X));
            var y = Math.Max(canvasBounds.Top, Math.Min(canvasBounds.Bottom, point.Y));
            return new Point(
                (x - canvasBounds.Left) * _canvasWidth / Math.Max(1, canvasBounds.Width),
                (y - canvasBounds.Top) * _canvasHeight / Math.Max(1, canvasBounds.Height));
        }

        private Point ToMiniMapPoint(Point point, Rect content)
        {
            return new Point(
                content.Left + point.X * content.Width / _canvasWidth,
                content.Top + point.Y * content.Height / _canvasHeight);
        }

        private Rect GetContentBounds()
        {
            return new Rect(
                ContentPadding,
                ContentPadding,
                Math.Max(1, ActualWidth - ContentPadding * 2),
                Math.Max(1, ActualHeight - ContentPadding * 2));
        }

        private Rect GetCanvasBounds(Rect content)
        {
            var scale = Math.Min(content.Width / _canvasWidth, content.Height / _canvasHeight);
            var width = _canvasWidth * scale;
            var height = _canvasHeight * scale;
            return new Rect(
                content.Left + (content.Width - width) / 2.0,
                content.Top + (content.Height - height) / 2.0,
                width,
                height);
        }

        private static double NormalizeExtent(double value)
        {
            return IsFinite(value) && value > 0 ? value : 1.0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
