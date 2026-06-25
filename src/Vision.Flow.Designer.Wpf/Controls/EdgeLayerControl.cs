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
    // 连线层控件渲染贝塞尔连线和连线预览。
    public sealed class EdgeLayerControl : Canvas
    {
        private const double NodeCardWidth = 190;
        private const double PortAnchorY = 43;

        private bool _hasPreview;
        private Point _previewStart;
        private Point _previewEnd;

        public EdgeLayerControl()
        {
            Width = FlowViewState.DefaultCanvasWidth;
            Height = FlowViewState.DefaultCanvasHeight;
            IsHitTestVisible = true;
        }

        public event Action<EdgeDefinition> EdgeSelected;

        public event Action<EdgeDefinition> EdgeDeleteRequested;

        public void SetCanvasSize(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public void Render(FlowDesignDocument document, EdgeDefinition selectedEdge, IDictionary<string, Point> portAnchors)
        {
            Children.Clear();
            if (document == null || document.Runtime == null || document.View == null)
            {
                return;
            }

            foreach (var edge in document.Runtime.Edges)
            {
                NodeViewState from;
                NodeViewState to;
                if (!document.View.Nodes.TryGetValue(edge.FromNodeId, out from) ||
                    !document.View.Nodes.TryGetValue(edge.ToNodeId, out to))
                {
                    continue;
                }

                var fallbackStart = new Point(from.X + NodeCardWidth, from.Y + PortAnchorY);
                var fallbackEnd = new Point(to.X, to.Y + PortAnchorY);
                var start = GetPortAnchor(
                    portAnchors,
                    FlowDesignerControl.CreatePortAnchorKey(edge.FromNodeId, "Output", edge.FromPort),
                    FlowDesignerControl.CreatePortAnchorKey(edge.FromNodeId, "Output", null),
                    fallbackStart);
                var end = GetPortAnchor(
                    portAnchors,
                    FlowDesignerControl.CreatePortAnchorKey(edge.ToNodeId, "Input", edge.ToPort),
                    FlowDesignerControl.CreatePortAnchorKey(edge.ToNodeId, "Input", null),
                    fallbackEnd);
                Children.Add(CreateEdgeVisual(start, end, edge, FlowDesignerControl.EdgeEquals(edge, selectedEdge)));
            }

            RenderPreview();
        }

        public void SetPreview(Point start, Point end)
        {
            _hasPreview = true;
            _previewStart = start;
            _previewEnd = end;
            RenderPreview();
        }

        public void ClearPreview()
        {
            _hasPreview = false;
            RenderPreview();
        }

        private void RenderPreview()
        {
            for (var index = Children.Count - 1; index >= 0; index--)
            {
                var element = Children[index] as FrameworkElement;
                if (element != null && string.Equals(Convert.ToString(element.Tag, CultureInfo.InvariantCulture), "__preview", StringComparison.Ordinal))
                {
                    Children.RemoveAt(index);
                }
            }

            if (_hasPreview)
            {
                Children.Add(CreatePreviewVisual(_previewStart, _previewEnd));
            }
        }

        private UIElement CreateEdgeVisual(Point start, Point end, EdgeDefinition edge, bool isSelected)
        {
            var geometry = CreateBezierGeometry(start, end);
            var stroke = isSelected
                ? FlowDesignerControl.BrushFromRgb(16, 185, 129)
                : FlowDesignerControl.BrushFromRgb(203, 213, 225);

            var group = new Canvas
            {
                Width = Width,
                Height = Height,
                Background = null,
                Tag = edge,
                ToolTip = FlowDesignerControl.FormatEdgeLabel(edge)
            };

            var hitPath = CreatePath(geometry, Brushes.Transparent, 13, null);
            hitPath.Cursor = Cursors.Hand;
            group.Children.Add(hitPath);

            var visiblePath = CreatePath(geometry, stroke, isSelected ? 2.4 : 1.6, null);
            visiblePath.IsHitTestVisible = false;
            group.Children.Add(visiblePath);

            group.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                SelectEdge(edge);
                e.Handled = true;
            };
            group.MouseRightButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                SelectEdge(edge);
            };
            group.ContextMenu = CreateEdgeContextMenu(edge);
            return group;
        }

        private static Point GetPortAnchor(IDictionary<string, Point> anchors, string exactKey, string fallbackKey, Point fallback)
        {
            Point point;
            if (anchors != null && !string.IsNullOrWhiteSpace(exactKey) && anchors.TryGetValue(exactKey, out point))
            {
                return point;
            }

            if (anchors != null && !string.IsNullOrWhiteSpace(fallbackKey) && anchors.TryGetValue(fallbackKey, out point))
            {
                return point;
            }

            return fallback;
        }

        private UIElement CreatePreviewVisual(Point start, Point end)
        {
            var stroke = FlowDesignerControl.BrushFromRgb(16, 185, 129);
            var geometry = CreateBezierGeometry(start, end);
            var group = new Canvas
            {
                Width = Width,
                Height = Height,
                IsHitTestVisible = false,
                Tag = "__preview"
            };
            var path = CreatePath(geometry, stroke, 1.8, new DoubleCollection { 4, 4 });
            group.Children.Add(path);
            return group;
        }

        private ContextMenu CreateEdgeContextMenu(EdgeDefinition edge)
        {
            var menu = new ContextMenu();
            var delete = new MenuItem { Header = "Delete" };
            delete.Click += delegate
            {
                var handler = EdgeDeleteRequested;
                if (handler != null)
                {
                    handler(edge);
                }
            };
            menu.Items.Add(delete);
            return menu;
        }

        private void SelectEdge(EdgeDefinition edge)
        {
            var handler = EdgeSelected;
            if (handler != null)
            {
                handler(edge);
            }
        }

        private static ShapesPath CreatePath(PathGeometry geometry, Brush stroke, double thickness, DoubleCollection dashArray)
        {
            return new ShapesPath
            {
                Data = geometry,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeDashArray = dashArray,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = null
            };
        }

        private static PathGeometry CreateBezierGeometry(Point start, Point end)
        {
            var distance = Math.Max(72, Math.Abs(end.X - start.X) * 0.45);
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = start,
                IsClosed = false,
                IsFilled = false
            };
            figure.Segments.Add(new BezierSegment(
                new Point(start.X + distance, start.Y),
                new Point(end.X - distance, end.Y),
                end,
                true));
            geometry.Figures.Add(figure);

            return geometry;
        }
    }
}
