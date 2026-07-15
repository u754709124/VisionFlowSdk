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
using Vision.Flow.Core.Runtime.Events;
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
    // 鏂囨。杈呭姪鏂规硶璐熻矗璁捐妯℃澘銆佽妭鐐圭紪杈戙€侀€夋嫨鐘舵€佸拰灞炴€у埛鏂般€?
    public sealed partial class FlowDesignerControl
    {
        /// <summary>
        /// 捕获当前设计态流程。捕获前会同步节点坐标、画布缩放和滚动偏移，
        /// 返回值为独立深拷贝，调用方修改它不会影响设计器中的当前文档。
        /// </summary>
        public FlowDesignDocument CaptureDocument()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(new Func<FlowDesignDocument>(CaptureDocument));
            }

            if (_document == null)
            {
                throw new InvalidOperationException("The designer does not contain a document.");
            }

            SaveRenderedNodeViewState();
            SaveCanvasViewState();
            return CloneDesignDocument(_document);
        }

        /// <summary>
        /// 由宿主加载完整设计态流程。加载前会停止当前调试运行并切回编辑模式，
        /// 传入文档会被深拷贝，后续由调用方持有的对象变化不会影响设计器。
        /// </summary>
        public Task LoadDocumentAsync(FlowDesignDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            var snapshot = CloneDesignDocument(document);
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher
                    .InvokeAsync(new Func<Task>(delegate { return LoadDocumentCoreAsync(snapshot); }))
                    .Task
                    .Unwrap();
            }

            return LoadDocumentCoreAsync(snapshot);
        }

        /// <summary>
        /// 由宿主创建空白设计态流程，不加载示例节点。
        /// </summary>
        public Task ResetDocumentAsync(string flowId, string flowName)
        {
            if (string.IsNullOrWhiteSpace(flowId))
            {
                throw new ArgumentException("FlowId is required.", "flowId");
            }

            if (string.IsNullOrWhiteSpace(flowName))
            {
                throw new ArgumentException("FlowName is required.", "flowName");
            }

            return LoadDocumentAsync(CreateDocument(flowId.Trim(), flowName.Trim()));
        }

        private async Task LoadDocumentCoreAsync(FlowDesignDocument document)
        {
            CancelConnectionPreview();
            await StopDebugAsync().ConfigureAwait(true);

            _interactionMode = DesignerInteractionMode.Edit;
            _document = document;
            _selectedNode = _document.Runtime.Nodes.FirstOrDefault();
            _selectedEdge = null;
            _nodeStartTimes.Clear();
            RenderCanvas();
            ApplyCanvasViewState();
            RenderProperties();
            _debug.Clear();
            AddDebugMessage("Design document loaded by host.");
            UpdateInteractionModeUi();
            UpdateStatus();
        }

        private void SaveRenderedNodeViewState()
        {
            if (_document == null || _document.View == null)
            {
                return;
            }

            if (_document.View.Nodes == null)
            {
                _document.View.Nodes = new Dictionary<string, NodeViewState>();
            }

            foreach (var item in _nodeCards)
            {
                var card = item.Value;
                if (card == null || card.ViewModel == null || card.ViewModel.Node == null)
                {
                    continue;
                }

                NodeViewState state;
                if (!_document.View.Nodes.TryGetValue(item.Key, out state) || state == null)
                {
                    state = new NodeViewState();
                    _document.View.Nodes[item.Key] = state;
                }

                var left = Canvas.GetLeft(card);
                var top = Canvas.GetTop(card);
                if (IsFinite(left))
                {
                    state.X = left;
                }

                if (IsFinite(top))
                {
                    state.Y = top;
                }
            }
        }

        private static FlowDesignDocument CloneDesignDocument(FlowDesignDocument document)
        {
            return FlowDesignSerializer.Deserialize(FlowDesignSerializer.Serialize(document));
        }

        private void CreateNewDesign()
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("New design skipped: switch to Edit mode first.");
                return;
            }

            _document = CreateDocument("designer-flow", "Designer Flow");
            _selectedNode = null;
            _selectedEdge = null;
            RenderCanvas();
            ApplyCanvasViewState();
            RenderProperties();
            _debug.Clear();
            AddDebugMessage("New design created.");
        }

        private void LoadCoreBasicTemplate()
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Sample load skipped: switch to Edit mode first.");
                return;
            }

            _document = CreateDocument("designer-core-basic", "Core Basic Flow");
            var flow = _document.Runtime;

            AddTemplateNode("set_result", FlowNodeTypes.VariableSet, "Set Result", 80, 120, new Dictionary<string, object>
            {
                { FlowSettingNames.VariableName, "Inspection.Result" },
                { FlowSettingNames.Value, "OK" }
            });
            AddTemplateNode("condition_1", FlowNodeTypes.ConditionIf, "Check Result", 380, 120, new Dictionary<string, object>
            {
                { FlowSettingNames.LeftBinding, "{{ set_result.Value }}" },
                { FlowSettingNames.Operator, "Equal" },
                { FlowSettingNames.RightValue, "OK" }
            });
            AddTemplateNode("log_ok", FlowNodeTypes.LogWrite, "Log OK", 700, 40, new Dictionary<string, object>
            {
                { FlowSettingNames.Level, "Info" },
                { FlowSettingNames.Message, "Inspection result is OK." }
            });
            AddTemplateNode("log_ng", FlowNodeTypes.LogWrite, "Log NG", 700, 220, new Dictionary<string, object>
            {
                { FlowSettingNames.Level, "Warning" },
                { FlowSettingNames.Message, "Inspection result is not OK." }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = DefaultEntryName, TargetNodeId = "set_result" });
            AddEdge("set_result", "condition_1");
            AddEdge("condition_1", FlowPortNames.True, "log_ok", FlowPortNames.In);
            AddEdge("condition_1", FlowPortNames.False, "log_ng", FlowPortNames.In);

            _selectedNode = flow.Nodes.FirstOrDefault();
            _selectedEdge = null;
            RenderCanvas();
            ApplyCanvasViewState();
            RenderProperties();
            _debug.Clear();
            AddDebugMessage("Core basic sample loaded.");
        }

        private void AddTemplateNode(
            string nodeId,
            string nodeType,
            string nodeName,
            double x,
            double y,
            IDictionary<string, object> settings)
        {
            var descriptor = GetDescriptor(nodeType);
            var node = new NodeDefinition
            {
                Id = nodeId,
                Type = nodeType,
                Name = nodeName,
                Version = descriptor == null ? "1.0.0" : descriptor.Version
            };

            foreach (var setting in settings)
            {
                node.Settings[setting.Key] = setting.Value;
            }

            _document.Runtime.Nodes.Add(node);
            _document.View.Nodes[nodeId] = new NodeViewState { X = x, Y = y };
        }

        private FlowDesignDocument CreateDocument(string flowId, string flowName)
        {
            return new FlowDesignDocument
            {
                FlowId = flowId,
                FlowName = flowName,
                Runtime = new RuntimeFlowDefinition
                {
                    FlowId = flowId,
                    FlowName = flowName,
                    Version = "1.0.0"
                },
                View = new FlowViewState()
            };
        }

        private void AddNodeFromPalette(NodeDescriptor descriptor)
        {
            AddNodeFromPalette(descriptor, null);
        }

        private void AddNodeFromPalette(NodeDescriptor descriptor, Point? canvasPosition)
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Add node skipped: switch to Edit mode first.");
                return;
            }

            if (descriptor == null)
            {
                return;
            }

            var nodeId = CreateNodeId(descriptor.NodeType);
            var node = new NodeDefinition
            {
                Id = nodeId,
                Type = descriptor.NodeType,
                Name = descriptor.DisplayName,
                Version = descriptor.Version
            };

            foreach (var setting in descriptor.Settings)
            {
                node.Settings[setting.Name] = CreateDefaultSettingValue(setting);
            }

            _document.Runtime.Nodes.Add(node);
            _document.View.Nodes[node.Id] = CreatePaletteNodeViewState(canvasPosition);

            if (_document.Runtime.Entries.Count == 0)
            {
                _document.Runtime.Entries.Add(new FlowEntryDefinition { EntryName = DefaultEntryName, TargetNodeId = node.Id });
            }

            SelectNode(node);
            RenderCanvas();
            AddDebugMessage("Added node " + node.Id + " (" + descriptor.NodeType + ").");
        }

        private NodeViewState CreatePaletteNodeViewState(Point? canvasPosition)
        {
            if (!canvasPosition.HasValue)
            {
                canvasPosition = GetDefaultPaletteNodePosition();
            }

            var x = Math.Max(8, SnapToGrid(canvasPosition.Value.X));
            var y = Math.Max(8, SnapToGrid(canvasPosition.Value.Y));
            ExpandCanvasForNewNode(ref x, ref y);
            return new NodeViewState { X = x, Y = y };
        }

        private Point GetDefaultPaletteNodePosition()
        {
            if (_canvasScroll == null)
            {
                return CreatePaletteFallbackGridPosition();
            }

            var viewportWidth = GetVisualExtent(_canvasScroll.ViewportWidth, _canvasScroll.ActualWidth, 0);
            var viewportHeight = GetVisualExtent(_canvasScroll.ViewportHeight, _canvasScroll.ActualHeight, 0);
            if (viewportWidth <= 0 || viewportHeight <= 0)
            {
                return CreatePaletteFallbackGridPosition();
            }

            return CalculateViewportCenteredNodePosition(
                _canvasScroll.HorizontalOffset,
                _canvasScroll.VerticalOffset,
                viewportWidth,
                viewportHeight,
                GetStoredCanvasZoom(),
                NodeBoundsFallbackWidth,
                NodeBoundsFallbackHeight);
        }

        private Point CreatePaletteFallbackGridPosition()
        {
            return new Point(
                80 + (_document.Runtime.Nodes.Count % 4) * 280,
                80 + (_document.Runtime.Nodes.Count / 4) * 170);
        }

        private object CreateDefaultSettingValue(NodeSettingDescriptor setting)
        {
            if (setting == null)
            {
                return null;
            }

            if (setting.DefaultValue != null)
            {
                return setting.DefaultValue;
            }

            if (StringEquals(setting.Name, FlowSettingNames.Message))
            {
                return "Debug node executed.";
            }

            return null;
        }

        private void AddEdge(string fromNodeId, string toNodeId)
        {
            AddEdge(fromNodeId, FlowPortNames.Next, toNodeId, FlowPortNames.In);
        }

        private void AddEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            if (!CanEditDocument)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
            {
                return;
            }

            var exists = _document.Runtime.Edges.Any(x =>
                StringEquals(x.FromNodeId, fromNodeId) &&
                StringEquals(x.ToNodeId, toNodeId) &&
                StringEquals(x.FromPort, fromPort) &&
                StringEquals(x.ToPort, toPort));
            if (exists)
            {
                return;
            }

            _document.Runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = fromNodeId,
                FromPort = string.IsNullOrWhiteSpace(fromPort) ? FlowPortNames.Next : fromPort,
                ToNodeId = toNodeId,
                ToPort = string.IsNullOrWhiteSpace(toPort) ? FlowPortNames.In : toPort
            });
        }

        private void SelectEdge(EdgeDefinition edge)
        {
            if (edge == null)
            {
                return;
            }

            Focus();
            _selectedEdge = edge;
            _selectedNode = null;
            foreach (var item in _nodeCards)
            {
                item.Value.SetSelected(false);
            }

            RenderProperties();
            RenderEdges();
            UpdateStatus();
        }

        private void DeleteSelection()
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Delete skipped: switch to Edit mode first.");
                return;
            }

            if (_selectedEdge != null)
            {
                DeleteEdge(_selectedEdge);
                return;
            }

            if (_selectedNode != null)
            {
                DeleteNode(_selectedNode);
            }
        }

        private void DeleteEdge(EdgeDefinition edge)
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Delete edge skipped: switch to Edit mode first.");
                return;
            }

            if (edge == null || _document == null || _document.Runtime == null)
            {
                return;
            }

            CancelConnectionPreview();
            _document.Runtime.Edges.RemoveAll(x => object.ReferenceEquals(x, edge) || EdgeEquals(x, edge));
            if (EdgeEquals(_selectedEdge, edge))
            {
                _selectedEdge = null;
            }

            RenderEdges();
            UpdateStatus();
            AddDebugMessage("Deleted edge " + edge.FromNodeId + " -> " + edge.ToNodeId + ".");
        }

        private void RenderCanvas()
        {
            EnsureCanvasContainsNodes();
            _nodeLayer.Children.Clear();
            _nodeCards.Clear();

            if (_document == null || _document.Runtime == null)
            {
                return;
            }

            foreach (var node in _document.Runtime.Nodes)
            {
                NodeViewState view;
                if (!_document.View.Nodes.TryGetValue(node.Id, out view))
                {
                    view = new NodeViewState { X = 80, Y = 80 };
                    _document.View.Nodes[node.Id] = view;
                }

                var descriptor = GetDescriptor(node.Type);
                var card = new NodeCardControl(new NodeViewModel(node, descriptor));
                card.SetSelected(StringEquals(node.Id, _selectedNode == null ? null : _selectedNode.Id));
                card.SetDisabled(IsNodeDisabled(node));
                card.SetEditEnabled(CanEditDocument);
                card.MouseLeftButtonDown += OnNodeMouseDown;
                card.MouseMove += OnNodeMouseMove;
                card.MouseLeftButtonUp += OnNodeMouseUp;
                card.OutputPortDragStarted += OnOutputPortDragStarted;
                card.InputPortDragCompleted += OnInputPortDragCompleted;
                card.ContextMenu = CanEditDocument ? CreateNodeContextMenu(node) : null;
                Canvas.SetLeft(card, view.X);
                Canvas.SetTop(card, view.Y);
                _nodeLayer.Children.Add(card);
                _nodeCards[node.Id] = card;
            }

            _hasDeferredEdgeRefresh = true;
            RenderEdges();
            UpdateStatus();
        }

        private void RenderProperties()
        {
            _properties.ShowNode(
                _selectedNode,
                _selectedNode == null ? null : GetDescriptor(_selectedNode.Type),
                CreateVariableSuggestions(_selectedNode),
                delegate
                {
                    if (!CanEditDocument)
                    {
                        return;
                    }

                    var card = _selectedNode == null || !_nodeCards.ContainsKey(_selectedNode.Id)
                        ? null
                        : _nodeCards[_selectedNode.Id];
                    if (card != null)
                    {
                        card.UpdateSummary();
                    }

                    RenderCanvas();
                },
                !CanEditDocument);
        }

        private IList<string> CreateVariableSuggestions(NodeDefinition currentNode)
        {
            var items = new List<string>();
            AddTokenVariableSuggestions(items);

            if (_document == null || _document.Runtime == null || _document.Runtime.Nodes == null)
            {
                return items;
            }

            foreach (var node in _document.Runtime.Nodes)
            {
                if (node == null ||
                    string.IsNullOrWhiteSpace(node.Id) ||
                    (currentNode != null && StringEquals(node.Id, currentNode.Id)))
                {
                    continue;
                }

                var descriptor = GetDescriptor(node.Type);
                if (descriptor == null || descriptor.Outputs == null)
                {
                    continue;
                }

                foreach (var output in descriptor.Outputs)
                {
                    if (!string.IsNullOrWhiteSpace(output.Name))
                    {
                        AddVariableSuggestion(items, node.Id, output.Name);
                    }
                }
            }

            return items
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.StartsWith("{{ token.", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddTokenVariableSuggestions(ICollection<string> items)
        {
            AddVariableSuggestion(items, "token", "TokenId");
            AddVariableSuggestion(items, "token", "ProductId");
            AddVariableSuggestion(items, "token", "WorkpieceId");
            AddVariableSuggestion(items, "token", "PositionId");
            AddVariableSuggestion(items, "token", "TriggerId");
            AddVariableSuggestion(items, "token", "FrameId");
            AddVariableSuggestion(items, "token", "Image");
            AddVariableSuggestion(items, "token", "Frame");
        }

        private static void AddVariableSuggestion(ICollection<string> items, string scope, string name)
        {
            if (items == null || string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            items.Add("{{ " + scope + "." + name + " }}");
        }

        private void SelectNode(NodeDefinition node)
        {
            _selectedNode = node;
            _selectedEdge = null;
            foreach (var item in _nodeCards)
            {
                item.Value.SetSelected(StringEquals(item.Key, node == null ? null : node.Id));
            }

            RenderEdges();
            RenderProperties();
            UpdateStatus();
        }

        private void SelectNodeById(string nodeId)
        {
            if (_document == null || _document.Runtime == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var node = _document.Runtime.Nodes.FirstOrDefault(x => StringEquals(x.Id, nodeId));
            if (node == null)
            {
                return;
            }

            SelectNode(node);
            NodeViewState view;
            if (_document.View != null && _document.View.Nodes.TryGetValue(node.Id, out view) && _canvasScroll != null)
            {
                _canvasScroll.ScrollToHorizontalOffset(Math.Max(0, view.X - 80));
                _canvasScroll.ScrollToVerticalOffset(Math.Max(0, view.Y - 80));
            }
        }

        private ContextMenu CreateNodeContextMenu(NodeDefinition node)
        {
            if (!CanEditDocument)
            {
                return null;
            }

            var menu = new ContextMenu();
            menu.Items.Add(CreateMenuItem("Rename", delegate { RenameNode(node); }));
            menu.Items.Add(CreateMenuItem("Duplicate", delegate { DuplicateNode(node); }));
            menu.Items.Add(CreateMenuItem(IsNodeDisabled(node) ? "Enable" : "Disable", delegate { ToggleNodeDisabled(node); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("Delete", delegate { DeleteNode(node); }));
            return menu;
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler click)
        {
            var item = new MenuItem { Header = header };
            item.Click += click;
            return item;
        }

        private void RenameNode(NodeDefinition node)
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Rename skipped: switch to Edit mode first.");
                return;
            }

            if (node == null)
            {
                return;
            }

            var dialog = new Window
            {
                Title = "Rename Node",
                Width = 360,
                Height = 150,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var layout = new StackPanel { Margin = new Thickness(16) };
            var textBox = new TextBox
            {
                Text = node.Name,
                MinHeight = 28,
                Padding = new Thickness(6, 4, 6, 4)
            };
            layout.Children.Add(textBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var ok = new Button { Content = "OK", MinWidth = 70, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", MinWidth = 70, Height = 28 };
            ok.Click += delegate { dialog.DialogResult = true; };
            cancel.Click += delegate { dialog.DialogResult = false; };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            layout.Children.Add(buttons);
            dialog.Content = layout;

            if (dialog.ShowDialog() == true)
            {
                node.Name = string.IsNullOrWhiteSpace(textBox.Text) ? node.Id : textBox.Text.Trim();
                RenderCanvas();
                SelectNode(node);
            }
        }

        private void DuplicateNode(NodeDefinition node)
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Duplicate skipped: switch to Edit mode first.");
                return;
            }

            if (node == null || _document == null || _document.Runtime == null)
            {
                return;
            }

            var clone = new NodeDefinition
            {
                Id = CreateNodeId(node.Type),
                Type = node.Type,
                Name = (string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name) + " Copy",
                Version = node.Version
            };

            foreach (var setting in node.Settings)
            {
                clone.Settings[setting.Key] = setting.Value;
            }

            foreach (var binding in node.InputBindings)
            {
                clone.InputBindings[binding.Key] = CloneBinding(binding.Value);
            }

            _document.Runtime.Nodes.Add(clone);
            NodeViewState view;
            if (!_document.View.Nodes.TryGetValue(node.Id, out view))
            {
                view = new NodeViewState { X = 80, Y = 80 };
            }

            _document.View.Nodes[clone.Id] = new NodeViewState
            {
                X = SnapToGrid(view.X + 48),
                Y = SnapToGrid(view.Y + 48)
            };
            RenderCanvas();
            SelectNode(clone);
            AddDebugMessage("Duplicated node " + node.Id + " as " + clone.Id + ".");
        }

        private void DeleteNode(NodeDefinition node)
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Delete node skipped: switch to Edit mode first.");
                return;
            }

            if (node == null || _document == null || _document.Runtime == null)
            {
                return;
            }

            _document.Runtime.Nodes.Remove(node);
            _document.Runtime.Edges.RemoveAll(x => StringEquals(x.FromNodeId, node.Id) || StringEquals(x.ToNodeId, node.Id));
            _selectedEdge = null;
            if (_document.View != null)
            {
                _document.View.Nodes.Remove(node.Id);
            }

            if (_selectedNode == node)
            {
                _selectedNode = _document.Runtime.Nodes.FirstOrDefault();
            }

            RenderCanvas();
            RenderProperties();
            AddDebugMessage("Deleted node " + node.Id + ".");
        }

        private void ToggleNodeDisabled(NodeDefinition node)
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Toggle disabled skipped: switch to Edit mode first.");
                return;
            }

            if (node == null)
            {
                return;
            }

            node.Settings[FlowSettingNames.Disabled] = !IsNodeDisabled(node);
            RenderCanvas();
            SelectNode(node);
        }

        private static bool IsNodeDisabled(NodeDefinition node)
        {
            object value;
            return node != null &&
                node.Settings != null &&
                node.Settings.TryGetValue(FlowSettingNames.Disabled, out value) &&
                value != null &&
                Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static VariableBinding CloneBinding(VariableBinding source)
        {
            if (source == null)
            {
                return null;
            }

            return new VariableBinding
            {
                Expression = source.Expression,
                SourceNodeId = source.SourceNodeId,
                SourceOutputName = source.SourceOutputName,
                ConstantValue = source.ConstantValue,
                ValueType = source.ValueType,
                IsConstant = source.IsConstant
            };
        }
    }
}
