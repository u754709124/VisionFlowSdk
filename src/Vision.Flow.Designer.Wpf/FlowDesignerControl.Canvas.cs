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
    // 画布辅助方法管理缩放、平移、节点拖拽、端口锚点和连线渲染。
    public sealed partial class FlowDesignerControl
    {
        private void ApplyCanvasViewState()
        {
            if (_document == null || _document.View == null || _canvasScale == null)
            {
                return;
            }

            var zoom = ClampZoom(_document.View.Zoom <= 0 ? 1.0 : _document.View.Zoom);
            _canvasScale.ScaleX = zoom;
            _canvasScale.ScaleY = zoom;
            UpdateZoomText();
            UpdateStatus();

            if (_canvasScroll != null)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _canvasScroll.ScrollToHorizontalOffset(Math.Max(0, _document.View.OffsetX));
                    _canvasScroll.ScrollToVerticalOffset(Math.Max(0, _document.View.OffsetY));
                }));
            }
        }

        private void SaveCanvasViewState()
        {
            if (_document == null || _document.View == null || _canvasScale == null)
            {
                return;
            }

            _document.View.Zoom = _canvasScale.ScaleX;
            if (_canvasScroll != null)
            {
                _document.View.OffsetX = _canvasScroll.HorizontalOffset;
                _document.View.OffsetY = _canvasScroll.VerticalOffset;
            }
        }

        private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ChangeCanvasZoom(e.Delta > 0 ? 1.1 : 0.9);
            e.Handled = true;
        }

        private void ChangeCanvasZoom(double factor)
        {
            if (_canvasScale == null)
            {
                return;
            }

            SetCanvasZoom(_canvasScale.ScaleX * factor);
        }

        private void SetCanvasZoom(double zoom)
        {
            if (_canvasScale == null || _document == null)
            {
                return;
            }

            var clamped = ClampZoom(zoom);
            _canvasScale.ScaleX = clamped;
            _canvasScale.ScaleY = clamped;
            SaveCanvasViewState();
            UpdateZoomText();
            UpdateStatus();
        }

        private void UpdateZoomText()
        {
            if (_zoomText == null)
            {
                return;
            }

            var zoom = _canvasScale == null ? 1.0 : _canvasScale.ScaleX;
            _zoomText.Text = Math.Round(zoom * 100.0).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private void OnSurfaceMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            if (e.ChangedButton == MouseButton.Left && !_isConnecting)
            {
                SelectNode(null);
                BeginCanvasPan(e);
                e.Handled = true;
            }
        }

        private void BeginCanvasPan(MouseButtonEventArgs e)
        {
            if (_canvasScroll == null || _surface == null)
            {
                return;
            }

            _isPanning = true;
            _panStart = e.GetPosition(this);
            _panStartHorizontalOffset = _canvasScroll.HorizontalOffset;
            _panStartVerticalOffset = _canvasScroll.VerticalOffset;
            _surface.CaptureMouse();
            _surface.Cursor = Cursors.Hand;
        }

        private void OnSurfaceMouseMove(object sender, MouseEventArgs e)
        {
            if (_isConnecting)
            {
                _edges.SetPreview(_connectionStartPoint, e.GetPosition(_nodeLayer));
            }

            if (!_isPanning || _canvasScroll == null)
            {
                return;
            }

            var point = e.GetPosition(this);
            _canvasScroll.ScrollToHorizontalOffset(_panStartHorizontalOffset - (point.X - _panStart.X));
            _canvasScroll.ScrollToVerticalOffset(_panStartVerticalOffset - (point.Y - _panStart.Y));
            SaveCanvasViewState();
            e.Handled = true;
        }

        private void OnSurfaceMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                if (_surface != null)
                {
                    _surface.ReleaseMouseCapture();
                    _surface.Cursor = Cursors.Hand;
                }

                e.Handled = true;
            }

            if (_isConnecting)
            {
                CompleteConnectionAt(e.GetPosition(_nodeLayer));
                e.Handled = true;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Delete || e.Key == Key.Back) && !IsTextEditorFocused())
            {
                DeleteSelection();
                e.Handled = true;
            }
        }

        private static double ClampZoom(double zoom)
        {
            return Math.Max(0.35, Math.Min(2.5, zoom));
        }

        private void RenderEdges()
        {
            if (_isRenderingEdges)
            {
                return;
            }

            try
            {
                _isRenderingEdges = true;
                if (_nodeLayer != null)
                {
                    _nodeLayer.UpdateLayout();
                }

                _edges.Render(_document, _selectedEdge, CreatePortAnchorMap());
            }
            finally
            {
                _isRenderingEdges = false;
            }
        }

        private void OnNodeLayerLayoutUpdated(object sender, EventArgs e)
        {
            if (!_hasDeferredEdgeRefresh || _isRenderingEdges)
            {
                return;
            }

            _hasDeferredEdgeRefresh = false;
            RenderEdges();
        }

        private IDictionary<string, Point> CreatePortAnchorMap()
        {
            var anchors = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _nodeCards)
            {
                var card = item.Value;
                if (card == null)
                {
                    continue;
                }

                AddPortAnchors(anchors, item.Key, "Input", card.InputPortControls);
                AddPortAnchors(anchors, item.Key, "Output", card.OutputPortControls);
            }

            return anchors;
        }

        private void AddPortAnchors(IDictionary<string, Point> anchors, string nodeId, string direction, IEnumerable<PortControl> ports)
        {
            if (anchors == null || string.IsNullOrWhiteSpace(nodeId) || ports == null)
            {
                return;
            }

            PortControl firstPort = null;
            foreach (var port in ports)
            {
                if (port == null || port.Port == null || port.Visibility != Visibility.Visible)
                {
                    continue;
                }

                if (firstPort == null)
                {
                    firstPort = port;
                }

                var center = GetPortCenter(port);
                if (!IsFinite(center.X) || !IsFinite(center.Y))
                {
                    continue;
                }

                anchors[CreatePortAnchorKey(nodeId, direction, port.Port.Name)] = center;
            }

            if (firstPort != null)
            {
                var center = GetPortCenter(firstPort);
                if (IsFinite(center.X) && IsFinite(center.Y))
                {
                    anchors[CreatePortAnchorKey(nodeId, direction, null)] = center;
                }
            }
        }

        private void OnOutputPortDragStarted(object sender, PortConnectionEventArgs e)
        {
            var card = sender as NodeCardControl;
            if (card == null || e == null || e.Port == null || e.PortControl == null)
            {
                return;
            }

            SelectNode(card.ViewModel.Node);
            _isConnecting = true;
            _connectionSourceNode = card.ViewModel.Node;
            _connectionSourcePort = e.Port.Name;
            _connectionStartPoint = GetPortCenter(e.PortControl);
            _edges.SetPreview(_connectionStartPoint, _connectionStartPoint);
            if (_surface != null)
            {
                _surface.CaptureMouse();
                _surface.Cursor = Cursors.Cross;
            }
        }

        private void OnInputPortDragCompleted(object sender, PortConnectionEventArgs e)
        {
            var card = sender as NodeCardControl;
            if (!_isConnecting || card == null || e == null || e.Port == null || _connectionSourceNode == null)
            {
                return;
            }

            CompleteConnection(card.ViewModel.Node, e.Port.Name);
        }

        private void CompleteConnectionAt(Point point)
        {
            var target = FindInputPortNear(point);
            if (target == null)
            {
                CancelConnectionPreview();
                return;
            }

            CompleteConnection(target.Node, target.Port.Name);
        }

        private void CompleteConnection(NodeDefinition targetNode, string targetPort)
        {
            if (!_isConnecting || _connectionSourceNode == null)
            {
                return;
            }

            if (targetNode == null || StringEquals(targetNode.Id, _connectionSourceNode.Id))
            {
                CancelConnectionPreview();
                return;
            }

            AddEdge(_connectionSourceNode.Id, _connectionSourcePort, targetNode.Id, targetPort);
            AddDebugMessage("Connected " + _connectionSourceNode.Id + "." + _connectionSourcePort + " -> " + targetNode.Id + "." + targetPort + ".");
            CancelConnectionPreview();
            RenderCanvas();
        }

        private PortHit FindInputPortNear(Point point)
        {
            var direct = FindInputPortFromHitTest(point);
            if (direct != null)
            {
                return direct;
            }

            PortHit nearest = null;
            var nearestDistance = 28.0;
            foreach (var item in _nodeCards)
            {
                var card = item.Value;
                if (card == null)
                {
                    continue;
                }

                foreach (var port in card.InputPortControls)
                {
                    if (port == null || !port.IsVisible)
                    {
                        continue;
                    }

                    var center = GetPortCenter(port);
                    var distance = (center - point).Length;
                    if (distance <= nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = new PortHit(card.ViewModel.Node, port.Port, port);
                    }
                }
            }

            return nearest;
        }

        private PortHit FindInputPortFromHitTest(Point point)
        {
            if (_nodeLayer == null)
            {
                return null;
            }

            var hit = VisualTreeHelper.HitTest(_nodeLayer, point);
            if (hit == null)
            {
                return null;
            }

            var port = FindAncestor<PortControl>(hit.VisualHit);
            if (port == null || port.Port == null || !string.Equals(port.Port.Direction, "Input", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var card = FindAncestor<NodeCardControl>(port);
            return card == null ? null : new PortHit(card.ViewModel.Node, port.Port, port);
        }

        private Point GetPortCenter(PortControl port)
        {
            return port.GetAnchorPoint(_nodeLayer);
        }

        private void CancelConnectionPreview()
        {
            _isConnecting = false;
            _connectionSourceNode = null;
            _connectionSourcePort = null;
            _edges.ClearPreview();
            if (_surface != null && _surface.IsMouseCaptured && !_isPanning)
            {
                _surface.ReleaseMouseCapture();
                _surface.Cursor = Cursors.Hand;
            }
        }

        private void OnNodeMouseDown(object sender, MouseButtonEventArgs e)
        {
            var card = sender as NodeCardControl;
            if (card == null)
            {
                return;
            }

            Focus();
            SelectNode(card.ViewModel.Node);
            if (e.ClickCount == 2)
            {
                RenameNode(card.ViewModel.Node);
                e.Handled = true;
                return;
            }

            _dragCard = card;
            _dragOffset = e.GetPosition(card);
            card.CaptureMouse();
            e.Handled = true;
        }

        private void OnNodeMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragCard == null || !_dragCard.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var point = e.GetPosition(_nodeLayer);
            var x = Math.Max(8, SnapToGrid(point.X - _dragOffset.X));
            var y = Math.Max(8, SnapToGrid(point.Y - _dragOffset.Y));
            Canvas.SetLeft(_dragCard, x);
            Canvas.SetTop(_dragCard, y);

            var node = _dragCard.ViewModel.Node;
            if (node != null && _document.View.Nodes.ContainsKey(node.Id))
            {
                _document.View.Nodes[node.Id].X = x;
                _document.View.Nodes[node.Id].Y = y;
            }

            RenderEdges();
        }

        private void OnNodeMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragCard != null)
            {
                _dragCard.ReleaseMouseCapture();
                _dragCard = null;
            }

            e.Handled = true;
        }

        private static double SnapToGrid(double value)
        {
            return Math.Round(value / GridSize, MidpointRounding.AwayFromZero) * GridSize;
        }

        private sealed class PortHit
        {
            public PortHit(NodeDefinition node, PortViewModel port, PortControl portControl)
            {
                Node = node;
                Port = port;
                PortControl = portControl;
            }

            public NodeDefinition Node { get; private set; }

            public PortViewModel Port { get; private set; }

            public PortControl PortControl { get; private set; }
        }
    }
}
