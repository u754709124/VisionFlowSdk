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
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
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

            EnsureCanvasContainsNodes();
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
            _document.View.CanvasWidth = _canvasWidth;
            _document.View.CanvasHeight = _canvasHeight;
            if (_canvasScroll != null)
            {
                _document.View.OffsetX = _canvasScroll.HorizontalOffset;
                _document.View.OffsetY = _canvasScroll.VerticalOffset;
            }
        }

        private void ApplyCanvasSizeFromView()
        {
            if (_document == null || _document.View == null)
            {
                return;
            }

            _canvasWidth = NormalizeCanvasExtent(_document.View.CanvasWidth, FlowViewState.DefaultCanvasWidth);
            _canvasHeight = NormalizeCanvasExtent(_document.View.CanvasHeight, FlowViewState.DefaultCanvasHeight);
            _document.View.CanvasWidth = _canvasWidth;
            _document.View.CanvasHeight = _canvasHeight;

            if (_surface != null)
            {
                _surface.Width = _canvasWidth;
                _surface.Height = _canvasHeight;
            }

            if (_gridLayer != null)
            {
                _gridLayer.Width = _canvasWidth;
                _gridLayer.Height = _canvasHeight;
            }

            if (_nodeLayer != null)
            {
                _nodeLayer.Width = _canvasWidth;
                _nodeLayer.Height = _canvasHeight;
            }

            if (_edges != null)
            {
                _edges.SetCanvasSize(_canvasWidth, _canvasHeight);
            }
        }

        private void EnsureCanvasContainsNodes()
        {
            if (_document == null || _document.View == null)
            {
                return;
            }

            ApplyCanvasSizeFromView();
            if (_document.View.Nodes == null)
            {
                _document.View.Nodes = new Dictionary<string, NodeViewState>();
            }

            if (_document.View.Nodes.Count == 0)
            {
                return;
            }

            var hasBounds = false;
            var left = double.MaxValue;
            var top = double.MaxValue;
            var right = double.MinValue;
            var bottom = double.MinValue;
            foreach (var state in _document.View.Nodes.Values)
            {
                if (state == null)
                {
                    continue;
                }

                if (!IsFinite(state.X))
                {
                    state.X = CanvasExpansionMargin;
                }

                if (!IsFinite(state.Y))
                {
                    state.Y = CanvasExpansionMargin;
                }

                left = Math.Min(left, state.X);
                top = Math.Min(top, state.Y);
                right = Math.Max(right, state.X + NodeBoundsFallbackWidth);
                bottom = Math.Max(bottom, state.Y + NodeBoundsFallbackHeight);
                hasBounds = true;
            }

            if (!hasBounds)
            {
                return;
            }

            var shiftX = left < CanvasExpansionMargin
                ? GetCanvasExpansionAmount(CanvasExpansionMargin - left)
                : 0;
            var shiftY = top < CanvasExpansionMargin
                ? GetCanvasExpansionAmount(CanvasExpansionMargin - top)
                : 0;

            if (shiftX > 0 || shiftY > 0)
            {
                // 左侧或上侧扩张时整体平移设计态坐标，避免保存负坐标。
                TranslateDesignNodes(shiftX, shiftY);
                _document.View.CanvasWidth += shiftX;
                _document.View.CanvasHeight += shiftY;
                var zoom = GetStoredCanvasZoom();
                _document.View.OffsetX = Math.Max(0, _document.View.OffsetX + shiftX * zoom);
                _document.View.OffsetY = Math.Max(0, _document.View.OffsetY + shiftY * zoom);
                right += shiftX;
                bottom += shiftY;
            }

            var requiredWidth = right + CanvasExpansionMargin;
            var requiredHeight = bottom + CanvasExpansionMargin;
            if (requiredWidth > _document.View.CanvasWidth)
            {
                _document.View.CanvasWidth = ExpandCanvasExtent(_document.View.CanvasWidth, requiredWidth, FlowViewState.DefaultCanvasWidth);
            }

            if (requiredHeight > _document.View.CanvasHeight)
            {
                _document.View.CanvasHeight = ExpandCanvasExtent(_document.View.CanvasHeight, requiredHeight, FlowViewState.DefaultCanvasHeight);
            }

            ApplyCanvasSizeFromView();
        }

        private void ExpandCanvasForNode(ref double x, ref double y, FrameworkElement nodeVisual)
        {
            if (_document == null || _document.View == null || nodeVisual == null)
            {
                return;
            }

            ApplyCanvasSizeFromView();
            var nodeWidth = GetVisualExtent(nodeVisual.ActualWidth, nodeVisual.Width, NodeBoundsFallbackWidth);
            var nodeHeight = GetVisualExtent(nodeVisual.ActualHeight, nodeVisual.MinHeight, NodeBoundsFallbackHeight);
            var shiftX = x < CanvasExpansionMargin
                ? GetCanvasExpansionAmount(CanvasExpansionMargin - x)
                : 0;
            var shiftY = y < CanvasExpansionMargin
                ? GetCanvasExpansionAmount(CanvasExpansionMargin - y)
                : 0;

            if (shiftX > 0 || shiftY > 0)
            {
                // 同步移动当前渲染层和滚动偏移，让视口看起来只是向左/上长出新空间。
                TranslateDesignNodes(shiftX, shiftY);
                ShiftRenderedNodeCards(shiftX, shiftY);
                _document.View.CanvasWidth += shiftX;
                _document.View.CanvasHeight += shiftY;
                x += shiftX;
                y += shiftY;

                var zoom = _canvasScale == null ? GetStoredCanvasZoom() : _canvasScale.ScaleX;
                if (_canvasScroll != null)
                {
                    _canvasScroll.ScrollToHorizontalOffset(_canvasScroll.HorizontalOffset + shiftX * zoom);
                    _canvasScroll.ScrollToVerticalOffset(_canvasScroll.VerticalOffset + shiftY * zoom);
                }

                _document.View.OffsetX = Math.Max(0, _document.View.OffsetX + shiftX * zoom);
                _document.View.OffsetY = Math.Max(0, _document.View.OffsetY + shiftY * zoom);
            }

            var requiredWidth = x + nodeWidth + CanvasExpansionMargin;
            var requiredHeight = y + nodeHeight + CanvasExpansionMargin;
            if (requiredWidth > _document.View.CanvasWidth)
            {
                _document.View.CanvasWidth = ExpandCanvasExtent(_document.View.CanvasWidth, requiredWidth, FlowViewState.DefaultCanvasWidth);
            }

            if (requiredHeight > _document.View.CanvasHeight)
            {
                _document.View.CanvasHeight = ExpandCanvasExtent(_document.View.CanvasHeight, requiredHeight, FlowViewState.DefaultCanvasHeight);
            }

            ApplyCanvasSizeFromView();
        }

        private void ExpandCanvasForNewNode(ref double x, ref double y)
        {
            if (_document == null || _document.View == null)
            {
                return;
            }

            ApplyCanvasSizeFromView();
            var shiftX = x < CanvasExpansionMargin
                ? GetCanvasExpansionAmount(CanvasExpansionMargin - x)
                : 0;
            var shiftY = y < CanvasExpansionMargin
                ? GetCanvasExpansionAmount(CanvasExpansionMargin - y)
                : 0;

            if (shiftX > 0 || shiftY > 0)
            {
                // 从节点库拖入新节点时，沿用节点拖拽的画布扩张规则，保持坐标均为非负设计态坐标。
                TranslateDesignNodes(shiftX, shiftY);
                ShiftRenderedNodeCards(shiftX, shiftY);
                _document.View.CanvasWidth += shiftX;
                _document.View.CanvasHeight += shiftY;
                x += shiftX;
                y += shiftY;

                var zoom = _canvasScale == null ? GetStoredCanvasZoom() : _canvasScale.ScaleX;
                if (_canvasScroll != null)
                {
                    _canvasScroll.ScrollToHorizontalOffset(_canvasScroll.HorizontalOffset + shiftX * zoom);
                    _canvasScroll.ScrollToVerticalOffset(_canvasScroll.VerticalOffset + shiftY * zoom);
                }

                _document.View.OffsetX = Math.Max(0, _document.View.OffsetX + shiftX * zoom);
                _document.View.OffsetY = Math.Max(0, _document.View.OffsetY + shiftY * zoom);
            }

            var requiredWidth = x + NodeBoundsFallbackWidth + CanvasExpansionMargin;
            var requiredHeight = y + NodeBoundsFallbackHeight + CanvasExpansionMargin;
            if (requiredWidth > _document.View.CanvasWidth)
            {
                _document.View.CanvasWidth = ExpandCanvasExtent(_document.View.CanvasWidth, requiredWidth, FlowViewState.DefaultCanvasWidth);
            }

            if (requiredHeight > _document.View.CanvasHeight)
            {
                _document.View.CanvasHeight = ExpandCanvasExtent(_document.View.CanvasHeight, requiredHeight, FlowViewState.DefaultCanvasHeight);
            }

            ApplyCanvasSizeFromView();
        }

        private void TranslateDesignNodes(double offsetX, double offsetY)
        {
            if (_document == null || _document.View == null || _document.View.Nodes == null)
            {
                return;
            }

            foreach (var state in _document.View.Nodes.Values)
            {
                if (state == null)
                {
                    continue;
                }

                state.X += offsetX;
                state.Y += offsetY;
            }
        }

        private void ShiftRenderedNodeCards(double offsetX, double offsetY)
        {
            foreach (var card in _nodeCards.Values)
            {
                if (card == null)
                {
                    continue;
                }

                var left = Canvas.GetLeft(card);
                var top = Canvas.GetTop(card);
                Canvas.SetLeft(card, (IsFinite(left) ? left : 0) + offsetX);
                Canvas.SetTop(card, (IsFinite(top) ? top : 0) + offsetY);
            }
        }

        private double GetStoredCanvasZoom()
        {
            if (_document != null && _document.View != null && _document.View.Zoom > 0)
            {
                return ClampZoom(_document.View.Zoom);
            }

            return _canvasScale == null ? 1.0 : _canvasScale.ScaleX;
        }

        private static double NormalizeCanvasExtent(double value, double defaultValue)
        {
            if (!IsFinite(value) || value < defaultValue)
            {
                return defaultValue;
            }

            return SnapUpToGrid(value);
        }

        private static double ExpandCanvasExtent(double current, double required, double defaultValue)
        {
            var normalizedCurrent = NormalizeCanvasExtent(current, defaultValue);
            if (required <= normalizedCurrent)
            {
                return normalizedCurrent;
            }

            return normalizedCurrent + GetCanvasExpansionAmount(required - normalizedCurrent);
        }

        private static double GetCanvasExpansionAmount(double required)
        {
            if (required <= 0)
            {
                return 0;
            }

            return Math.Ceiling(required / CanvasExpansionStep) * CanvasExpansionStep;
        }

        private static double SnapUpToGrid(double value)
        {
            return Math.Ceiling(value / GridSize) * GridSize;
        }

        private static double GetVisualExtent(double actual, double configured, double fallback)
        {
            if (IsFinite(actual) && actual > 0)
            {
                return actual;
            }

            if (IsFinite(configured) && configured > 0)
            {
                return configured;
            }

            return fallback;
        }

        private static Point CalculateViewportCenteredNodePosition(
            double horizontalOffset,
            double verticalOffset,
            double viewportWidth,
            double viewportHeight,
            double zoom,
            double nodeWidth,
            double nodeHeight)
        {
            var safeZoom = zoom <= 0 || !IsFinite(zoom) ? 1.0 : zoom;
            var centerX = (horizontalOffset + viewportWidth / 2.0) / safeZoom;
            var centerY = (verticalOffset + viewportHeight / 2.0) / safeZoom;
            return new Point(centerX - nodeWidth / 2.0, centerY - nodeHeight / 2.0);
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

        private void OnPaletteNodeDragRequested(object sender, NodePaletteDragEventArgs e)
        {
            if (!CanEditDocument || e == null || e.Descriptor == null || e.DragSource == null)
            {
                return;
            }

            var data = new DataObject();
            data.SetData(typeof(NodeDescriptor), e.Descriptor);
            data.SetData(PaletteNodeTypeDragFormat, e.Descriptor.NodeType);
            DragDrop.DoDragDrop(e.DragSource, data, DragDropEffects.Copy);
        }

        private void OnPaletteNodeDragOver(object sender, DragEventArgs e)
        {
            e.Effects = CanEditDocument && GetPaletteDragDescriptor(e.Data) != null
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnPaletteNodeDrop(object sender, DragEventArgs e)
        {
            var descriptor = GetPaletteDragDescriptor(e.Data);
            if (!CanEditDocument || descriptor == null || _nodeLayer == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            AddNodeFromPalette(descriptor, e.GetPosition(_nodeLayer));
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private NodeDescriptor GetPaletteDragDescriptor(IDataObject data)
        {
            if (data == null)
            {
                return null;
            }

            if (data.GetDataPresent(typeof(NodeDescriptor)))
            {
                var descriptor = data.GetData(typeof(NodeDescriptor)) as NodeDescriptor;
                if (descriptor != null)
                {
                    return descriptor;
                }
            }

            if (!data.GetDataPresent(PaletteNodeTypeDragFormat))
            {
                return null;
            }

            var nodeType = Convert.ToString(data.GetData(PaletteNodeTypeDragFormat), CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(nodeType) ? null : GetDescriptor(nodeType);
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
            if (_isConnecting && CanEditDocument)
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
            if (CanEditDocument && (e.Key == Key.Delete || e.Key == Key.Back) && !IsTextEditorFocused())
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
            if (!CanEditDocument)
            {
                return;
            }

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
            if (!CanEditDocument)
            {
                return;
            }

            var card = sender as NodeCardControl;
            if (!_isConnecting || card == null || e == null || e.Port == null || _connectionSourceNode == null)
            {
                return;
            }

            CompleteConnection(card.ViewModel.Node, e.Port.Name);
        }

        private void CompleteConnectionAt(Point point)
        {
            if (!CanEditDocument)
            {
                CancelConnectionPreview();
                return;
            }

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
            if (!CanEditDocument)
            {
                CancelConnectionPreview();
                return;
            }

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
            if (!CanEditDocument)
            {
                e.Handled = true;
                return;
            }

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
            if (!CanEditDocument)
            {
                return;
            }

            if (_dragCard == null || !_dragCard.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var point = e.GetPosition(_nodeLayer);
            var x = Math.Max(8, SnapToGrid(point.X - _dragOffset.X));
            var y = Math.Max(8, SnapToGrid(point.Y - _dragOffset.Y));
            ExpandCanvasForNode(ref x, ref y, _dragCard);
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
