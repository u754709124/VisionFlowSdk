using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Designer.Wpf
{
    public sealed class FlowDesignerControl : UserControl
    {
        private const string DefaultEntryName = "ManualStart";
        private const double CanvasWidth = 1800;
        private const double CanvasHeight = 1100;

        private readonly NodeRegistry _nodeRegistry;
        private readonly Dictionary<string, NodeCardControl> _nodeCards;
        private readonly NodePaletteControl _palette;
        private readonly PropertyPanelControl _properties;
        private readonly RuntimeDebugPanelControl _debug;
        private readonly EdgeLayerControl _edges;
        private readonly Canvas _nodeLayer;
        private readonly TextBlock _statusText;

        private FlowDesignDocument _document;
        private NodeDefinition _selectedNode;
        private IFlowRunner _runner;
        private Point _dragOffset;
        private NodeCardControl _dragCard;

        public FlowDesignerControl()
        {
            _nodeRegistry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(_nodeRegistry);
            _nodeCards = new Dictionary<string, NodeCardControl>(StringComparer.OrdinalIgnoreCase);
            _palette = new NodePaletteControl();
            _properties = new PropertyPanelControl();
            _debug = new RuntimeDebugPanelControl();
            _edges = new EdgeLayerControl();
            _nodeLayer = new Canvas
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                Background = Brushes.Transparent
            };
            _statusText = new TextBlock
            {
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            DebugDevices = null;
            InitializeResources();
            Content = CreateShell();
            _palette.SetDescriptors(_nodeRegistry.Descriptors.OrderBy(x => x.Category).ThenBy(x => x.DisplayName));
            _palette.NodeRequested += AddNodeFromPalette;
            LoadSingleShotTemplate();
        }

        public IDeviceRegistry DebugDevices { get; set; }

        private void InitializeResources()
        {
            Resources["FlowPageBackground"] = BrushFromRgb(246, 248, 252);
            Resources["FlowPanelBackground"] = Brushes.White;
            Resources["FlowPanelBorder"] = BrushFromRgb(222, 229, 238);
            Resources["FlowAccent"] = BrushFromRgb(22, 101, 52);
            Resources["FlowText"] = BrushFromRgb(17, 24, 39);
            Resources["FlowMutedText"] = BrushFromRgb(100, 116, 139);
        }

        private UIElement CreateShell()
        {
            var root = new Grid
            {
                Background = (Brush)Resources["FlowPageBackground"]
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(170) });

            var toolbar = CreateToolbar();
            Grid.SetRow(toolbar, 0);
            root.Children.Add(toolbar);

            var workspace = new Grid
            {
                Margin = new Thickness(12, 10, 12, 8)
            };
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
            Grid.SetRow(workspace, 1);

            Grid.SetColumn(_palette, 0);
            workspace.Children.Add(_palette);

            var canvasPanel = CreateCanvasPanel();
            Grid.SetColumn(canvasPanel, 1);
            workspace.Children.Add(canvasPanel);

            Grid.SetColumn(_properties, 2);
            workspace.Children.Add(_properties);
            root.Children.Add(workspace);

            Grid.SetRow(_debug, 2);
            root.Children.Add(_debug);

            return root;
        }

        private UIElement CreateToolbar()
        {
            var root = new Border
            {
                Background = BrushFromRgb(15, 23, 42),
                BorderBrush = BrushFromRgb(15, 23, 42),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var dock = new DockPanel();
            root.Child = dock;

            var statusBorder = new Border
            {
                Background = BrushFromRgb(30, 41, 59),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 32,
                Child = _statusText
            };
            DockPanel.SetDock(statusBorder, Dock.Right);
            dock.Children.Add(statusBorder);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            dock.Children.Add(buttons);

            buttons.Children.Add(CreateToolbarButton("New", delegate { CreateNewDesign(); }));
            buttons.Children.Add(CreateToolbarButton("Sample", delegate { LoadSingleShotTemplate(); }));
            buttons.Children.Add(CreateToolbarButton("Open", delegate { OpenDesign(); }));
            buttons.Children.Add(CreateToolbarButton("Save", delegate { SaveDesign(); }));
            buttons.Children.Add(CreateToolbarButton("Publish", delegate { PublishRuntime(); }));
            buttons.Children.Add(CreateToolbarButton("Debug Run", async delegate { await RunDebugAsync(); }));
            buttons.Children.Add(CreateToolbarButton("Stop", async delegate { await StopDebugAsync(); }));

            return root;
        }

        private UIElement CreateCanvasPanel()
        {
            var border = CreatePanelBorder(new Thickness(10, 0, 10, 0));
            border.Padding = new Thickness(0);

            var surface = new Grid
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                Background = BrushFromRgb(248, 250, 252)
            };
            surface.Children.Add(CreateGridLayer());
            surface.Children.Add(_edges);
            surface.Children.Add(_nodeLayer);

            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = surface
            };
            border.Child = scroll;

            return border;
        }

        private Canvas CreateGridLayer()
        {
            var grid = new Canvas
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                IsHitTestVisible = false
            };

            for (var x = 0; x <= CanvasWidth; x += 32)
            {
                grid.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = CanvasHeight,
                    Stroke = BrushFromRgb(235, 240, 247),
                    StrokeThickness = x % 128 == 0 ? 1.2 : 0.6
                });
            }

            for (var y = 0; y <= CanvasHeight; y += 32)
            {
                grid.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = CanvasWidth,
                    Y2 = y,
                    Stroke = BrushFromRgb(235, 240, 247),
                    StrokeThickness = y % 128 == 0 ? 1.2 : 0.6
                });
            }

            return grid;
        }

        private void CreateNewDesign()
        {
            _document = CreateDocument("designer-flow", "Designer Flow");
            _selectedNode = null;
            RenderCanvas();
            RenderProperties();
            _debug.Clear();
            AddDebugMessage("New design created.");
        }

        private void LoadSingleShotTemplate()
        {
            _document = CreateDocument("designer-single-shot", "Single Shot Inspection");
            var flow = _document.Runtime;

            AddTemplateNode("light_1", "light.control", "Light Control", 70, 90, new Dictionary<string, object>
            {
                { "LightId", "Light01" },
                { "Channels", CreateLightChannels("Main", 85) },
                { "StableDelayMs", 0 }
            });
            AddTemplateNode("trigger_1", "camera.soft_trigger", "Camera Trigger", 360, 90, new Dictionary<string, object>
            {
                { "CameraId", "Camera01" },
                { "TimeoutMs", 1000 }
            });
            AddTemplateNode("callback_1", "camera.image_callback", "Image Callback", 650, 90, new Dictionary<string, object>
            {
                { "CameraId", "Camera01" },
                { "MatchMode", "TriggerId" },
                { "TimeoutMs", 1500 }
            });
            AddTemplateNode("recipe_1", "recipe.run", "Recipe Run", 940, 90, new Dictionary<string, object>
            {
                { "RecipeId", "Recipe01" },
                { "InputImageBinding", "{{ callback_1.Image }}" },
                { "TimeoutMs", 5000 }
            });
            AddTemplateNode("save_1", "image.save", "Save Image", 1230, 90, new Dictionary<string, object>
            {
                { "SaverId", "ImageSave01" },
                { "ImageBinding", "{{ callback_1.Image }}" },
                { "ResultImageBinding", "{{ recipe_1.ResultImage }}" },
                { "FileNameTemplate", "{TokenId}_{ImageId}.png" }
            });
            AddTemplateNode("db_1", "database.save", "Save Result", 1230, 300, new Dictionary<string, object>
            {
                { "DatabaseId", "VisionDb" },
                { "TableName", "InspectionResults" },
                { "FieldMappings", CreateFieldMappings("IsOk={{ recipe_1.IsOk }};FrameId={{ callback_1.FrameId }};ImagePath={{ save_1.ImagePath }}") }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = DefaultEntryName, TargetNodeId = "light_1" });
            AddEdge("light_1", "trigger_1");
            AddEdge("trigger_1", "callback_1");
            AddEdge("callback_1", "recipe_1");
            AddEdge("recipe_1", "save_1");
            AddEdge("save_1", "db_1");

            _selectedNode = flow.Nodes.FirstOrDefault();
            RenderCanvas();
            RenderProperties();
            _debug.Clear();
            AddDebugMessage("Single-shot sample loaded.");
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

        private void AddNodeFromPalette(NodeDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return;
            }

            var previous = _document.Runtime.Nodes.LastOrDefault();
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

            AutoConfigureNode(node, previous);
            _document.Runtime.Nodes.Add(node);
            _document.View.Nodes[node.Id] = new NodeViewState
            {
                X = 80 + (_document.Runtime.Nodes.Count % 4) * 280,
                Y = 80 + (_document.Runtime.Nodes.Count / 4) * 170
            };

            if (_document.Runtime.Entries.Count == 0)
            {
                _document.Runtime.Entries.Add(new FlowEntryDefinition { EntryName = DefaultEntryName, TargetNodeId = node.Id });
            }

            if (previous != null)
            {
                AddEdge(previous.Id, node.Id);
            }

            SelectNode(node);
            RenderCanvas();
            AddDebugMessage("Added node " + node.Id + " (" + descriptor.NodeType + ").");
        }

        private void AutoConfigureNode(NodeDefinition node, NodeDefinition previous)
        {
            if (node == null)
            {
                return;
            }

            if (string.Equals(node.Type, "recipe.run", StringComparison.OrdinalIgnoreCase) &&
                previous != null &&
                string.Equals(previous.Type, "camera.image_callback", StringComparison.OrdinalIgnoreCase))
            {
                node.Settings["InputImageBinding"] = "{{ " + previous.Id + ".Image }}";
            }

            if (string.Equals(node.Type, "image.save", StringComparison.OrdinalIgnoreCase) && previous != null)
            {
                if (string.Equals(previous.Type, "camera.image_callback", StringComparison.OrdinalIgnoreCase))
                {
                    node.Settings["ImageBinding"] = "{{ " + previous.Id + ".Image }}";
                }

                if (string.Equals(previous.Type, "recipe.run", StringComparison.OrdinalIgnoreCase))
                {
                    node.Settings["ResultImageBinding"] = "{{ " + previous.Id + ".ResultImage }}";
                }
            }

            if (string.Equals(node.Type, "database.save", StringComparison.OrdinalIgnoreCase) && previous != null)
            {
                node.Settings["FieldMappings"] = CreateFieldMappings("PreviousNode=" + previous.Id);
            }
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

            if (StringEquals(setting.Name, "CameraId"))
            {
                return "Camera01";
            }

            if (StringEquals(setting.Name, "LightId"))
            {
                return "Light01";
            }

            if (StringEquals(setting.Name, "RecipeId"))
            {
                return "Recipe01";
            }

            if (StringEquals(setting.Name, "DatabaseId"))
            {
                return "VisionDb";
            }

            if (StringEquals(setting.Name, "TableName"))
            {
                return "InspectionResults";
            }

            if (StringEquals(setting.Name, "Channels"))
            {
                return CreateLightChannels("Main", 80);
            }

            if (StringEquals(setting.Name, "Parameters"))
            {
                return CreateCameraParameters("ExposureTime=1000;Gain=1");
            }

            if (StringEquals(setting.Name, "FieldMappings"))
            {
                return CreateFieldMappings("TokenId={{ token.TokenId }}");
            }

            if (StringEquals(setting.Name, "Message"))
            {
                return "Debug node executed.";
            }

            return null;
        }

        private void AddEdge(string fromNodeId, string toNodeId)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
            {
                return;
            }

            var exists = _document.Runtime.Edges.Any(x =>
                StringEquals(x.FromNodeId, fromNodeId) &&
                StringEquals(x.ToNodeId, toNodeId) &&
                StringEquals(x.FromPort, "Next"));
            if (exists)
            {
                return;
            }

            _document.Runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = fromNodeId,
                FromPort = "Next",
                ToNodeId = toNodeId,
                ToPort = "In"
            });
        }

        private void RenderCanvas()
        {
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
                card.MouseLeftButtonDown += OnNodeMouseDown;
                card.MouseMove += OnNodeMouseMove;
                card.MouseLeftButtonUp += OnNodeMouseUp;
                Canvas.SetLeft(card, view.X);
                Canvas.SetTop(card, view.Y);
                _nodeLayer.Children.Add(card);
                _nodeCards[node.Id] = card;
            }

            _edges.Render(_document);
            UpdateStatus();
        }

        private void RenderProperties()
        {
            _properties.ShowNode(
                _selectedNode,
                _selectedNode == null ? null : GetDescriptor(_selectedNode.Type),
                delegate
                {
                    var card = _selectedNode == null || !_nodeCards.ContainsKey(_selectedNode.Id)
                        ? null
                        : _nodeCards[_selectedNode.Id];
                    if (card != null)
                    {
                        card.UpdateSummary();
                    }

                    RenderCanvas();
                });
        }

        private void SelectNode(NodeDefinition node)
        {
            _selectedNode = node;
            foreach (var item in _nodeCards)
            {
                item.Value.SetSelected(StringEquals(item.Key, node == null ? null : node.Id));
            }

            RenderProperties();
            UpdateStatus();
        }

        private void OnNodeMouseDown(object sender, MouseButtonEventArgs e)
        {
            var card = sender as NodeCardControl;
            if (card == null)
            {
                return;
            }

            SelectNode(card.ViewModel.Node);
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
            var x = Math.Max(8, point.X - _dragOffset.X);
            var y = Math.Max(8, point.Y - _dragOffset.Y);
            Canvas.SetLeft(_dragCard, x);
            Canvas.SetTop(_dragCard, y);

            var node = _dragCard.ViewModel.Node;
            if (node != null && _document.View.Nodes.ContainsKey(node.Id))
            {
                _document.View.Nodes[node.Id].X = x;
                _document.View.Nodes[node.Id].Y = y;
            }

            _edges.Render(_document);
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

        private void OpenDesign()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Flow design (*.flowdesign)|*.flowdesign|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory()
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _document = FlowDesignSerializer.Load(dialog.FileName);
                if (_document.View == null)
                {
                    _document.View = new FlowViewState();
                }

                if (_document.Runtime == null)
                {
                    _document.Runtime = new RuntimeFlowDefinition();
                }

                _selectedNode = _document.Runtime.Nodes.FirstOrDefault();
                RenderCanvas();
                RenderProperties();
                AddDebugMessage("Opened " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Open failed: " + ex.Message);
            }
        }

        private void SaveDesign()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Flow design (*.flowdesign)|*.flowdesign|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory(),
                FileName = (_document.FlowId ?? "designer-flow") + ".flowdesign"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dialog.FileName));
                FlowDesignSerializer.Save(dialog.FileName, _document);
                AddDebugMessage("Saved design " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Save failed: " + ex.Message);
            }
        }

        private void PublishRuntime()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Flow runtime (*.flowruntime)|*.flowruntime|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory(),
                FileName = (_document.Runtime.FlowId ?? "designer-flow") + ".flowruntime"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dialog.FileName));
                RuntimeFlowSerializer.Save(dialog.FileName, _document.Runtime);
                AddDebugMessage("Published runtime " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Publish failed: " + ex.Message);
            }
        }

        private async Task RunDebugAsync()
        {
            if (_document == null || _document.Runtime == null || _document.Runtime.Nodes.Count == 0)
            {
                AddDebugMessage("Debug skipped: the flow has no nodes.");
                return;
            }

            try
            {
                await StopDebugAsync().ConfigureAwait(true);
                ResetNodeStates();

                var sink = new DesignerEventSink(this);
                var engine = new FlowEngine(_nodeRegistry, sink, DebugDevices);
                _runner = engine.CreateRunner(_document.Runtime);
                await _runner.StartAsync().ConfigureAwait(true);
                await _runner.TriggerAsync(DefaultEntryName, CreateDebugToken()).ConfigureAwait(true);
                await _runner.StopAsync().ConfigureAwait(true);
                AddDebugMessage("Debug run completed.");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Debug run failed: " + ex.Message);
            }
        }

        private async Task StopDebugAsync()
        {
            if (_runner == null)
            {
                return;
            }

            try
            {
                if (_runner.IsRunning)
                {
                    await _runner.StopAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                AddDebugMessage("Stop failed: " + ex.Message);
            }
            finally
            {
                _runner = null;
            }
        }

        private void ResetNodeStates()
        {
            foreach (var card in _nodeCards.Values)
            {
                card.SetRuntimeState(NodeRuntimeState.Waiting);
            }
        }

        private FlowToken CreateDebugToken()
        {
            return new FlowToken
            {
                ProductId = "DemoProduct",
                WorkpieceId = "WP-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture),
                PositionId = "P1",
                CaptureGroupId = "SingleShot"
            };
        }

        private void HandleRuntimeEvent(FlowRuntimeEvent runtimeEvent)
        {
            if (runtimeEvent == null)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { HandleRuntimeEvent(runtimeEvent); }));
                return;
            }

            _debug.AddEvent(runtimeEvent);
            if (!string.IsNullOrWhiteSpace(runtimeEvent.NodeId) && _nodeCards.ContainsKey(runtimeEvent.NodeId))
            {
                _nodeCards[runtimeEvent.NodeId].SetRuntimeState(runtimeEvent.State);
            }
        }

        private void AddDebugMessage(string message)
        {
            _debug.AddMessage(message);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var nodeCount = _document == null || _document.Runtime == null ? 0 : _document.Runtime.Nodes.Count;
            var edgeCount = _document == null || _document.Runtime == null ? 0 : _document.Runtime.Edges.Count;
            var selected = _selectedNode == null ? "none" : _selectedNode.Id;
            _statusText.Text = string.Format(CultureInfo.InvariantCulture, "{0} nodes | {1} edges | selected: {2}", nodeCount, edgeCount, selected);
        }

        private NodeDescriptor GetDescriptor(string nodeType)
        {
            INodeFactory factory;
            return _nodeRegistry.TryGetFactory(nodeType, out factory) ? factory.Descriptor : null;
        }

        private string CreateNodeId(string nodeType)
        {
            var prefix = string.IsNullOrWhiteSpace(nodeType) ? "node" : nodeType.Replace('.', '_').Replace('-', '_');
            var index = 1;
            string id;
            do
            {
                id = prefix + "_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }
            while (_document.Runtime.Nodes.Any(x => StringEquals(x.Id, id)));

            return id;
        }

        private static Border CreatePanelBorder(Thickness margin)
        {
            return new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(222, 229, 238),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = margin
            };
        }

        private static Button CreateToolbarButton(string text, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 74,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(10, 0, 10, 0),
                Background = BrushFromRgb(30, 41, 59),
                BorderBrush = BrushFromRgb(51, 65, 85),
                Foreground = Brushes.White
            };
            button.Click += handler;
            return button;
        }

        private static List<Dictionary<string, object>> CreateLightChannels(string channelName, double intensity)
        {
            return new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "ChannelName", channelName },
                    { "IsEnabled", true },
                    { "Intensity", intensity },
                    { "DurationMs", 0 }
                }
            };
        }

        private static List<Dictionary<string, object>> CreateCameraParameters(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var item in SplitPairs(text))
            {
                result.Add(new Dictionary<string, object>
                {
                    { "Name", item.Key },
                    { "Value", item.Value }
                });
            }

            return result;
        }

        private static List<Dictionary<string, object>> CreateFieldMappings(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var item in SplitPairs(text))
            {
                var mapping = new Dictionary<string, object>
                {
                    { "FieldName", item.Key }
                };

                if (item.Value != null && item.Value.Trim().StartsWith("{{", StringComparison.Ordinal))
                {
                    mapping["ValueBinding"] = item.Value;
                }
                else
                {
                    mapping["Value"] = item.Value;
                }

                result.Add(mapping);
            }

            return result;
        }

        private static IList<KeyValuePair<string, string>> SplitPairs(string text)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var parts = text.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var index = part.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                var key = part.Substring(0, index).Trim();
                var value = part.Substring(index + 1).Trim();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    result.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            return result;
        }

        private static string GetSampleFlowDirectory()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var i = 0; i < 8 && directory != null; i++)
            {
                var sampleDirectory = System.IO.Path.Combine(directory.FullName, "samples", "flows");
                if (Directory.Exists(sampleDirectory))
                {
                    return sampleDirectory;
                }

                directory = directory.Parent;
            }

            return Environment.CurrentDirectory;
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal static SolidColorBrush BrushFromRgb(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private sealed class DesignerEventSink : IFlowEventSink
        {
            private readonly FlowDesignerControl _owner;

            public DesignerEventSink(FlowDesignerControl owner)
            {
                _owner = owner;
            }

            public Task PublishAsync(FlowRuntimeEvent runtimeEvent, System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _owner.HandleRuntimeEvent(runtimeEvent);
                return Task.FromResult(0);
            }
        }
    }

    public sealed class NodePaletteControl : Border
    {
        private readonly StackPanel _items;

        public NodePaletteControl()
        {
            Margin = new Thickness(0);
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            var layout = new DockPanel();
            Child = layout;

            var title = CreateTitle("Node Palette");
            DockPanel.SetDock(title, Dock.Top);
            layout.Children.Add(title);

            _items = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _items
            };
            layout.Children.Add(scroll);
        }

        public event Action<NodeDescriptor> NodeRequested;

        public void SetDescriptors(IEnumerable<NodeDescriptor> descriptors)
        {
            _items.Children.Clear();
            string currentCategory = null;
            foreach (var descriptor in descriptors)
            {
                if (!string.Equals(currentCategory, descriptor.Category, StringComparison.OrdinalIgnoreCase))
                {
                    currentCategory = descriptor.Category;
                    _items.Children.Add(new TextBlock
                    {
                        Text = currentCategory,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                        Margin = new Thickness(0, 12, 0, 6)
                    });
                }

                var button = new Button
                {
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(9, 7, 9, 7),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = FlowDesignerControl.BrushFromRgb(248, 250, 252),
                    BorderBrush = FlowDesignerControl.BrushFromRgb(226, 232, 240),
                    Tag = descriptor,
                    Content = CreatePaletteContent(descriptor)
                };
                button.Click += delegate
                {
                    var handler = NodeRequested;
                    if (handler != null)
                    {
                        handler(descriptor);
                    }
                };
                _items.Children.Add(button);
            }
        }

        private static UIElement CreatePaletteContent(NodeDescriptor descriptor)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = descriptor.DisplayName,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42)
            });
            panel.Children.Add(new TextBlock
            {
                Text = descriptor.NodeType,
                FontSize = 11,
                Foreground = FlowDesignerControl.BrushFromRgb(100, 116, 139),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            return panel;
        }

        private static TextBlock CreateTitle(string title)
        {
            return new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }
    }

    public sealed class NodeCardControl : Border
    {
        private readonly TextBlock _title;
        private readonly TextBlock _type;
        private readonly TextBlock _summary;
        private readonly Border _stateChip;
        private readonly TextBlock _stateText;

        public NodeCardControl(NodeViewModel viewModel)
        {
            ViewModel = viewModel;
            Width = 218;
            MinHeight = 112;
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);
            Padding = new Thickness(10);
            Cursor = Cursors.SizeAll;

            var root = new DockPanel();
            Child = root;

            var ports = CreatePortRow(viewModel);
            DockPanel.SetDock(ports, Dock.Bottom);
            root.Children.Add(ports);

            var header = new DockPanel();
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var icon = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(5),
                Background = FlowDesignerControl.BrushFromRgb(220, 252, 231),
                Child = new TextBlock
                {
                    Text = "VF",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    Foreground = FlowDesignerControl.BrushFromRgb(22, 101, 52)
                }
            };
            DockPanel.SetDock(icon, Dock.Left);
            header.Children.Add(icon);

            _stateChip = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Background = FlowDesignerControl.BrushFromRgb(241, 245, 249),
                Child = _stateText = new TextBlock
                {
                    Text = "Ready",
                    FontSize = 11,
                    Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105)
                }
            };
            DockPanel.SetDock(_stateChip, Dock.Right);
            header.Children.Add(_stateChip);

            var text = new StackPanel
            {
                Margin = new Thickness(8, 0, 8, 0)
            };
            header.Children.Add(text);
            _title = new TextBlock
            {
                Text = viewModel.Node.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _type = new TextBlock
            {
                Text = viewModel.Node.Type,
                FontSize = 11,
                Foreground = FlowDesignerControl.BrushFromRgb(100, 116, 139),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            text.Children.Add(_title);
            text.Children.Add(_type);

            _summary = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 8),
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                MaxHeight = 34
            };
            root.Children.Add(_summary);
            UpdateSummary();
        }

        public NodeViewModel ViewModel { get; private set; }

        public void UpdateSummary()
        {
            _title.Text = string.IsNullOrWhiteSpace(ViewModel.Node.Name) ? ViewModel.Node.Id : ViewModel.Node.Name;
            _type.Text = ViewModel.Node.Type;

            var parts = new List<string>();
            foreach (var setting in ViewModel.Node.Settings.Take(3))
            {
                if (setting.Value == null)
                {
                    continue;
                }

                parts.Add(setting.Key + "=" + ToShortText(setting.Value));
            }

            _summary.Text = parts.Count == 0 ? "No settings" : string.Join(", ", parts.ToArray());
        }

        public void SetSelected(bool isSelected)
        {
            BorderBrush = isSelected
                ? FlowDesignerControl.BrushFromRgb(22, 101, 52)
                : FlowDesignerControl.BrushFromRgb(203, 213, 225);
            BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
        }

        public void SetRuntimeState(NodeRuntimeState state)
        {
            if (state == NodeRuntimeState.Running)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(254, 249, 195);
                _stateText.Foreground = FlowDesignerControl.BrushFromRgb(133, 77, 14);
                _stateText.Text = "Running";
                return;
            }

            if (state == NodeRuntimeState.Completed)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(220, 252, 231);
                _stateText.Foreground = FlowDesignerControl.BrushFromRgb(22, 101, 52);
                _stateText.Text = "Done";
                return;
            }

            if (state == NodeRuntimeState.Failed)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(254, 226, 226);
                _stateText.Foreground = FlowDesignerControl.BrushFromRgb(153, 27, 27);
                _stateText.Text = "Failed";
                return;
            }

            if (state == NodeRuntimeState.Timeout)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(255, 237, 213);
                _stateText.Foreground = FlowDesignerControl.BrushFromRgb(154, 52, 18);
                _stateText.Text = "Timeout";
                return;
            }

            _stateChip.Background = FlowDesignerControl.BrushFromRgb(241, 245, 249);
            _stateText.Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105);
            _stateText.Text = "Ready";
        }

        private static UIElement CreatePortRow(NodeViewModel viewModel)
        {
            var row = new DockPanel
            {
                LastChildFill = false
            };

            var input = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            foreach (var port in viewModel.InputPorts)
            {
                input.Children.Add(new PortControl(port));
            }

            DockPanel.SetDock(input, Dock.Left);
            row.Children.Add(input);

            var output = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            foreach (var port in viewModel.OutputPorts)
            {
                output.Children.Add(new PortControl(port));
            }

            DockPanel.SetDock(output, Dock.Right);
            row.Children.Add(output);
            return row;
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
        public PortControl(PortViewModel port)
        {
            Width = 14;
            Height = 14;
            CornerRadius = new CornerRadius(7);
            Margin = new Thickness(2);
            Background = port.Direction == "Input"
                ? FlowDesignerControl.BrushFromRgb(14, 165, 233)
                : FlowDesignerControl.BrushFromRgb(34, 197, 94);
            ToolTip = port.Name + " (" + port.DataType + ")";
        }
    }

    public sealed class EdgeLayerControl : Canvas
    {
        public EdgeLayerControl()
        {
            Width = 1800;
            Height = 1100;
            IsHitTestVisible = false;
        }

        public void Render(FlowDesignDocument document)
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

                Children.Add(new Line
                {
                    X1 = from.X + 218,
                    Y1 = from.Y + 82,
                    X2 = to.X,
                    Y2 = to.Y + 82,
                    Stroke = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                    StrokeThickness = 2.0,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Triangle
                });
            }
        }
    }

    public sealed class PropertyPanelControl : Border
    {
        private readonly StackPanel _rows;
        private NodeDefinition _node;
        private Action _changed;

        public PropertyPanelControl()
        {
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            _rows = new StackPanel();
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _rows
            };
        }

        public void ShowNode(NodeDefinition node, NodeDescriptor descriptor, Action changed)
        {
            _node = node;
            _changed = changed;
            _rows.Children.Clear();

            _rows.Children.Add(CreateTitle("Properties"));
            if (node == null)
            {
                _rows.Children.Add(CreateMutedText("Select a node on the canvas."));
                return;
            }

            AddTextField("Id", node.Id, false, null);
            AddTextField("Name", node.Name, true, delegate(string text)
            {
                node.Name = text;
            });
            AddTextField("Type", node.Type, false, null);

            _rows.Children.Add(CreateSection("Settings"));
            if (descriptor != null)
            {
                foreach (var setting in descriptor.Settings)
                {
                    object value;
                    node.Settings.TryGetValue(setting.Name, out value);
                    AddTextField(setting.DisplayName + " (" + setting.Name + ")", ToEditorText(setting, value), true, delegate(string text)
                    {
                        node.Settings[setting.Name] = ConvertFromEditorText(setting, text);
                    });
                }
            }

            if (descriptor != null && descriptor.InputPorts.Count > 0)
            {
                _rows.Children.Add(CreateSection("Input Bindings"));
                foreach (var input in descriptor.InputPorts)
                {
                    VariableBinding binding;
                    var text = node.InputBindings.TryGetValue(input.Name, out binding) && binding != null
                        ? binding.Expression
                        : string.Empty;
                    AddTextField(input.DisplayName + " (" + input.Name + ")", text, true, delegate(string value)
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            node.InputBindings.Remove(input.Name);
                        }
                        else
                        {
                            node.InputBindings[input.Name] = VariableBinding.ForExpression(value);
                        }
                    });
                }
            }

            if (descriptor != null && descriptor.Outputs.Count > 0)
            {
                _rows.Children.Add(CreateSection("Outputs"));
                foreach (var output in descriptor.Outputs)
                {
                    _rows.Children.Add(CreateMutedText(output.Name + " : " + output.DataType));
                }
            }
        }

        private void AddTextField(string label, string value, bool editable, Action<string> setter)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 8, 0, 3)
            });

            var textBox = new TextBox
            {
                Text = value ?? string.Empty,
                IsReadOnly = !editable,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = label.IndexOf("Mappings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("Channels", StringComparison.OrdinalIgnoreCase) >= 0,
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            textBox.LostFocus += delegate
            {
                if (setter != null)
                {
                    setter(textBox.Text);
                    if (_changed != null)
                    {
                        _changed();
                    }
                }
            };
            _rows.Children.Add(textBox);
        }

        private static TextBlock CreateTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private static TextBlock CreateSection(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 14, 0, 4)
            };
        }

        private static TextBlock CreateMutedText(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = FlowDesignerControl.BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 3)
            };
        }

        private static string ToEditorText(NodeSettingDescriptor setting, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (setting != null && string.Equals(setting.Name, "Channels", StringComparison.OrdinalIgnoreCase))
            {
                return ToPairText(value, "ChannelName", "Intensity");
            }

            if (setting != null && string.Equals(setting.Name, "Parameters", StringComparison.OrdinalIgnoreCase))
            {
                return ToPairText(value, "Name", "Value");
            }

            if (setting != null && string.Equals(setting.Name, "FieldMappings", StringComparison.OrdinalIgnoreCase))
            {
                return ToPairText(value, "FieldName", "ValueBinding", "Value");
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object ConvertFromEditorText(NodeSettingDescriptor setting, string text)
        {
            if (setting == null)
            {
                return text;
            }

            if (string.Equals(setting.Name, "Channels", StringComparison.OrdinalIgnoreCase))
            {
                return ParseChannels(text);
            }

            if (string.Equals(setting.Name, "Parameters", StringComparison.OrdinalIgnoreCase))
            {
                return ParseParameters(text);
            }

            if (string.Equals(setting.Name, "FieldMappings", StringComparison.OrdinalIgnoreCase))
            {
                return ParseFieldMappings(text);
            }

            if (string.Equals(setting.DataType, "Int32", StringComparison.OrdinalIgnoreCase))
            {
                int intValue;
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue) ? intValue : 0;
            }

            if (string.Equals(setting.DataType, "Double", StringComparison.OrdinalIgnoreCase))
            {
                double doubleValue;
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue) ? doubleValue : 0.0;
            }

            if (string.Equals(setting.DataType, "Boolean", StringComparison.OrdinalIgnoreCase))
            {
                bool boolValue;
                return bool.TryParse(text, out boolValue) && boolValue;
            }

            return string.IsNullOrWhiteSpace(text) && setting.IsRequired == false ? null : text;
        }

        private static List<Dictionary<string, object>> ParseChannels(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var pair in ParsePairs(text))
            {
                double intensity;
                double.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out intensity);
                result.Add(new Dictionary<string, object>
                {
                    { "ChannelName", pair.Key },
                    { "IsEnabled", true },
                    { "Intensity", intensity },
                    { "DurationMs", 0 }
                });
            }

            return result;
        }

        private static List<Dictionary<string, object>> ParseParameters(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var pair in ParsePairs(text))
            {
                result.Add(new Dictionary<string, object>
                {
                    { "Name", pair.Key },
                    { "Value", pair.Value }
                });
            }

            return result;
        }

        private static List<Dictionary<string, object>> ParseFieldMappings(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var pair in ParsePairs(text))
            {
                var mapping = new Dictionary<string, object>
                {
                    { "FieldName", pair.Key }
                };
                if (pair.Value != null && pair.Value.Trim().StartsWith("{{", StringComparison.Ordinal))
                {
                    mapping["ValueBinding"] = pair.Value;
                }
                else
                {
                    mapping["Value"] = pair.Value;
                }

                result.Add(mapping);
            }

            return result;
        }

        private static IList<KeyValuePair<string, string>> ParsePairs(string text)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var parts = text.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var index = part.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                result.Add(new KeyValuePair<string, string>(
                    part.Substring(0, index).Trim(),
                    part.Substring(index + 1).Trim()));
            }

            return result;
        }

        private static string ToPairText(object value, string keyName, string valueName)
        {
            return ToPairText(value, keyName, valueName, null);
        }

        private static string ToPairText(object value, string keyName, string valueName, string fallbackValueName)
        {
            var list = value as System.Collections.IEnumerable;
            if (list == null || value is string)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            var parts = new List<string>();
            foreach (var item in list)
            {
                var dictionary = item as System.Collections.IDictionary;
                if (dictionary == null)
                {
                    continue;
                }

                var key = GetDictionaryValue(dictionary, keyName);
                var pairValue = GetDictionaryValue(dictionary, valueName);
                if (pairValue == null && fallbackValueName != null)
                {
                    pairValue = GetDictionaryValue(dictionary, fallbackValueName);
                }

                if (key != null)
                {
                    parts.Add(Convert.ToString(key, CultureInfo.InvariantCulture) + "=" + Convert.ToString(pairValue, CultureInfo.InvariantCulture));
                }
            }

            return string.Join(";", parts.ToArray());
        }

        private static object GetDictionaryValue(System.Collections.IDictionary dictionary, string key)
        {
            foreach (System.Collections.DictionaryEntry item in dictionary)
            {
                if (string.Equals(Convert.ToString(item.Key, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }

            return null;
        }
    }

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
            layout.Children.Add(_events);
        }

        public void Clear()
        {
            _events.Items.Clear();
        }

        public void AddMessage(string message)
        {
            _events.Items.Add(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message);
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
            _events.Items.Add(text);
            _events.ScrollIntoView(_events.Items[_events.Items.Count - 1]);
        }
    }

    public sealed class FlowDesignerViewModel
    {
        public FlowDesignDocument Document { get; set; }
    }

    public sealed class NodeViewModel
    {
        public NodeViewModel(NodeDefinition node, NodeDescriptor descriptor)
        {
            Node = node;
            Descriptor = descriptor;
            InputPorts = descriptor == null
                ? new List<PortViewModel>()
                : descriptor.InputPorts.Select(x => new PortViewModel(x)).ToList();
            OutputPorts = descriptor == null
                ? new List<PortViewModel>()
                : descriptor.OutputPorts.Select(x => new PortViewModel(x)).ToList();
        }

        public NodeDefinition Node { get; private set; }

        public NodeDescriptor Descriptor { get; private set; }

        public IList<PortViewModel> InputPorts { get; private set; }

        public IList<PortViewModel> OutputPorts { get; private set; }
    }

    public sealed class PortViewModel
    {
        public PortViewModel(NodePortDescriptor descriptor)
        {
            Name = descriptor.Name;
            Direction = descriptor.Direction;
            DataType = descriptor.DataType;
        }

        public string Name { get; private set; }

        public string Direction { get; private set; }

        public string DataType { get; private set; }
    }

    public sealed class EdgeViewModel
    {
        public EdgeDefinition Edge { get; set; }
    }

    public sealed class PropertyPanelViewModel
    {
        public NodeDefinition SelectedNode { get; set; }
    }

    public sealed class NodePaletteViewModel
    {
        public IList<NodeDescriptor> Nodes { get; set; }
    }

    public sealed class RuntimeDebugViewModel
    {
        public RuntimeDebugViewModel()
        {
            Events = new List<FlowRuntimeEvent>();
        }

        public IList<FlowRuntimeEvent> Events { get; private set; }
    }
}
