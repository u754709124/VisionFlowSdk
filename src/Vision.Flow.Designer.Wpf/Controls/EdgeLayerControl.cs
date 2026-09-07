using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using ShapesPath = System.Windows.Shapes.Path;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>复用控制流连线视觉，并按节点邻接关系更新拖动期间的曲线。</summary>
    public sealed class EdgeLayerControl : Canvas
    {
        private const double NodeCardWidth = 220;
        private const double PortAnchorY = 78;
        private static readonly Brush NormalStroke = FlowDesignerControl.BrushFromRgb(203, 213, 225);
        private static readonly Brush SelectedStroke = FlowDesignerControl.BrushFromRgb(16, 185, 129);
        private readonly Dictionary<EdgeDefinition, EdgeVisual> _visuals = new Dictionary<EdgeDefinition, EdgeVisual>();
        private readonly Dictionary<string, List<EdgeVisual>> _adjacent = new Dictionary<string, List<EdgeVisual>>(StringComparer.OrdinalIgnoreCase);
        private ShapesPath _preview;
        private bool _isReadOnly;

        /// <summary>创建拥有自身曲线、命中区域和预览的画布层。</summary>
        public EdgeLayerControl()
        {
            Width = FlowViewState.DefaultCanvasWidth;
            Height = FlowViewState.DefaultCanvasHeight;
            IsHitTestVisible = true;
        }

        /// <summary>用户选中连线时通知宿主，参数为当前文档的连线对象。</summary>
        public event Action<EdgeDefinition> EdgeSelected;

        /// <summary>用户请求删除可编辑连线时通知宿主。</summary>
        public event Action<EdgeDefinition> EdgeDeleteRequested;

        /// <summary>同步现有及后续连线菜单的只读状态。</summary>
        public void SetReadOnly(bool isReadOnly)
        {
            _isReadOnly = isReadOnly;
            foreach (var visual in _visuals.Values)
                visual.DeleteMenu.IsEnabled = !isReadOnly;
        }

        /// <summary>同步画布尺寸，不重建曲线或命中对象。</summary>
        public void SetCanvasSize(double width, double height)
        {
            if (Width == width && Height == height)
                return;
            Width = width;
            Height = height;
            foreach (var visual in _visuals.Values)
            {
                visual.Group.Width = width;
                visual.Group.Height = height;
            }
        }

        /// <summary>同步文档结构、选择和端口位置，保留仍存在的连线视觉对象。</summary>
        public void Render(FlowDesignDocument document, EdgeDefinition selectedEdge, IDictionary<string, Point> portAnchors)
        {
            var retained = new HashSet<EdgeDefinition>();
            _adjacent.Clear();
            if (document != null && document.Runtime != null && document.View != null)
            {
                foreach (var edge in document.Runtime.Edges)
                {
                    if (!document.View.Nodes.ContainsKey(edge.FromNodeId) || !document.View.Nodes.ContainsKey(edge.ToNodeId))
                        continue;
                    retained.Add(edge);
                    EdgeVisual visual;
                    if (!_visuals.TryGetValue(edge, out visual))
                    {
                        visual = CreateEdgeVisual(edge);
                        _visuals.Add(edge, visual);
                        Children.Add(visual.Group);
                    }
                    visual.Group.ToolTip = FlowDesignerControl.FormatEdgeLabel(edge);
                    var selected = FlowDesignerControl.EdgeEquals(edge, selectedEdge);
                    visual.VisiblePath.Stroke = selected ? SelectedStroke : NormalStroke;
                    visual.VisiblePath.StrokeThickness = selected ? 2.4 : 1.6;
                    UpdateGeometry(visual, document, portAnchors);
                    AddAdjacent(edge.FromNodeId, visual);
                    if (!string.Equals(edge.FromNodeId, edge.ToNodeId, StringComparison.OrdinalIgnoreCase))
                        AddAdjacent(edge.ToNodeId, visual);
                }
            }
            foreach (var edge in _visuals.Keys.Where(x => !retained.Contains(x)).ToArray())
            {
                Children.Remove(_visuals[edge].Group);
                _visuals.Remove(edge);
            }
        }

        /// <summary>仅更新指定节点的关联边；结构变化必须先通过 Render 同步邻接索引。</summary>
        public void UpdateNodes(FlowDesignDocument document, IEnumerable<string> nodeIds, IDictionary<string, Point> portAnchors)
        {
            var changed = new HashSet<EdgeVisual>();
            foreach (var nodeId in nodeIds)
            {
                List<EdgeVisual> adjacent;
                if (_adjacent.TryGetValue(nodeId, out adjacent))
                    foreach (var visual in adjacent)
                        changed.Add(visual);
            }
            foreach (var visual in changed)
                UpdateGeometry(visual, document, portAnchors);
        }

        /// <summary>原位更新非交互的临时连线预览。</summary>
        public void SetPreview(Point start, Point end)
        {
            if (_preview == null)
            {
                _preview = CreatePath(SelectedStroke, 1.8);
                _preview.IsHitTestVisible = false;
                _preview.StrokeDashArray = new DoubleCollection { 4, 4 };
                SetZIndex(_preview, 1);
                Children.Add(_preview);
            }
            SetGeometry((PathGeometry)_preview.Data, start, end);
        }

        /// <summary>移除当前临时连线预览。</summary>
        public void ClearPreview()
        {
            if (_preview != null)
                Children.Remove(_preview);
            _preview = null;
        }

        private void AddAdjacent(string nodeId, EdgeVisual visual)
        {
            List<EdgeVisual> adjacent;
            if (!_adjacent.TryGetValue(nodeId, out adjacent))
            {
                adjacent = new List<EdgeVisual>();
                _adjacent.Add(nodeId, adjacent);
            }
            adjacent.Add(visual);
        }

        private static void UpdateGeometry(EdgeVisual visual, FlowDesignDocument document, IDictionary<string, Point> anchors)
        {
            var edge = visual.Edge;
            var from = document.View.Nodes[edge.FromNodeId];
            var to = document.View.Nodes[edge.ToNodeId];
            var start = GetPortAnchor(anchors, edge.FromNodeId, FlowPortDirection.Output, edge.FromPort, new Point(from.X + NodeCardWidth, from.Y + PortAnchorY));
            var end = GetPortAnchor(anchors, edge.ToNodeId, FlowPortDirection.Input, edge.ToPort, new Point(to.X, to.Y + PortAnchorY));
            SetGeometry(visual.Geometry, start, end);
        }

        private EdgeVisual CreateEdgeVisual(EdgeDefinition edge)
        {
            var hitPath = CreatePath(Brushes.Transparent, 13);
            hitPath.Cursor = Cursors.Hand;
            var visiblePath = CreatePath(NormalStroke, 1.6);
            visiblePath.Data = hitPath.Data;
            visiblePath.IsHitTestVisible = false;
            var group = new Canvas { Width = Width, Height = Height, Tag = edge };
            group.Children.Add(hitPath);
            group.Children.Add(visiblePath);
            group.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                SelectEdge(edge);
                e.Handled = true;
            };
            group.MouseRightButtonDown += delegate { SelectEdge(edge); };
            var delete = new MenuItem { Header = "Delete", IsEnabled = !_isReadOnly };
            delete.Click += delegate
            {
                if (!_isReadOnly)
                    EdgeDeleteRequested?.Invoke(edge);
            };
            group.ContextMenu = new ContextMenu();
            group.ContextMenu.Items.Add(delete);
            return new EdgeVisual { Edge = edge, Group = group, VisiblePath = visiblePath, Geometry = (PathGeometry)hitPath.Data, DeleteMenu = delete };
        }

        private static Point GetPortAnchor(IDictionary<string, Point> anchors, string nodeId, FlowPortDirection direction, string port, Point fallback)
        {
            Point point;
            if (anchors != null && (anchors.TryGetValue(FlowDesignerControl.CreatePortAnchorKey(nodeId, direction, port), out point) ||
                anchors.TryGetValue(FlowDesignerControl.CreatePortAnchorKey(nodeId, direction, null), out point)))
                return point;
            return fallback;
        }

        private void SelectEdge(EdgeDefinition edge)
        {
            EdgeSelected?.Invoke(edge);
        }

        private static ShapesPath CreatePath(Brush stroke, double thickness)
        {
            var figure = new PathFigure { IsClosed = false, IsFilled = false };
            figure.Segments.Add(new BezierSegment());
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return new ShapesPath { Data = geometry, Stroke = stroke, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        }

        private static void SetGeometry(PathGeometry geometry, Point start, Point end)
        {
            var figure = geometry.Figures[0];
            var segment = (BezierSegment)figure.Segments[0];
            if (figure.StartPoint == start && segment.Point3 == end)
                return;
            var distance = Math.Max(72, Math.Abs(end.X - start.X) * 0.45);
            figure.StartPoint = start;
            segment.Point1 = new Point(start.X + distance, start.Y);
            segment.Point2 = new Point(end.X - distance, end.Y);
            segment.Point3 = end;
        }

        // 事件闭包只持有该连线；结构移除时一起移出视觉树和索引，避免保留旧文档。
        private sealed class EdgeVisual
        {
            internal EdgeDefinition Edge;
            internal Canvas Group;
            internal ShapesPath VisiblePath;
            internal PathGeometry Geometry;
            internal MenuItem DeleteMenu;
        }
    }
}
