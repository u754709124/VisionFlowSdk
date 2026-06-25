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
    public sealed class FlowDesignerOptions
    {
        public FlowDesignerOptions()
        {
            LoadSampleOnStartup = true;
        }

        public bool LoadSampleOnStartup { get; set; }

        public IDeviceRegistry DebugDevices { get; set; }
    }

    public sealed class FlowDesignerControl : UserControl
    {
        private const string DefaultEntryName = "ManualStart";
        private const double CanvasWidth = 1800;
        private const double CanvasHeight = 1100;
        private const double GridSize = 32;

        private readonly NodeRegistry _nodeRegistry;
        private readonly Dictionary<string, NodeCardControl> _nodeCards;
        private readonly Dictionary<string, DateTime> _nodeStartTimes;
        private readonly NodePaletteControl _palette;
        private readonly PropertyPanelControl _properties;
        private readonly RuntimeDebugPanelControl _debug;
        private readonly EdgeLayerControl _edges;
        private readonly FlowDesignerOptions _options;
        private readonly Canvas _nodeLayer;
        private readonly TextBlock _statusText;
        private TextBlock _zoomText;

        private FlowDesignDocument _document;
        private NodeDefinition _selectedNode;
        private EdgeDefinition _selectedEdge;
        private IFlowRunner _runner;
        private Grid _surface;
        private ScrollViewer _canvasScroll;
        private ScaleTransform _canvasScale;
        private Point _dragOffset;
        private NodeCardControl _dragCard;
        private bool _isPanning;
        private Point _panStart;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;
        private bool _isRenderingEdges;
        private bool _hasDeferredEdgeRefresh;
        private bool _isConnecting;
        private NodeDefinition _connectionSourceNode;
        private string _connectionSourcePort;
        private Point _connectionStartPoint;

        public FlowDesignerControl()
            : this(null, null, null)
        {
        }

        public FlowDesignerControl(NodeRegistry nodeRegistry)
            : this(nodeRegistry, null, null)
        {
        }

        public FlowDesignerControl(NodeRegistry nodeRegistry, IDeviceRegistry debugDevices)
            : this(nodeRegistry, debugDevices, null)
        {
        }

        public FlowDesignerControl(NodeRegistry nodeRegistry, IDeviceRegistry debugDevices, FlowDesignerOptions options)
        {
            _options = options ?? new FlowDesignerOptions();
            _nodeRegistry = nodeRegistry ?? CreateDefaultNodeRegistry();
            _nodeCards = new Dictionary<string, NodeCardControl>(StringComparer.OrdinalIgnoreCase);
            _nodeStartTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            _palette = new NodePaletteControl();
            _properties = new PropertyPanelControl();
            _debug = new RuntimeDebugPanelControl();
            _edges = new EdgeLayerControl();
            _edges.EdgeSelected += SelectEdge;
            _edges.EdgeDeleteRequested += DeleteEdge;
            _nodeLayer = new Canvas
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                Background = null
            };
            _nodeLayer.LayoutUpdated += OnNodeLayerLayoutUpdated;
            _statusText = new TextBlock
            {
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            DebugDevices = debugDevices ?? _options.DebugDevices;
            InitializeResources();
            Content = CreateShell();
            _palette.SetDescriptors(_nodeRegistry.Descriptors.OrderBy(x => x.Category).ThenBy(x => x.DisplayName));
            _palette.NodeRequested += AddNodeFromPalette;
            _debug.NodeRequested += SelectNodeById;
            PreviewKeyDown += OnPreviewKeyDown;
            Focusable = true;
            if (_options.LoadSampleOnStartup)
            {
                LoadSingleShotTemplate();
            }
            else
            {
                CreateNewDesign();
            }
        }

        public IDeviceRegistry DebugDevices { get; set; }

        public NodeRegistry NodeRegistry
        {
            get { return _nodeRegistry; }
        }

        public FlowDesignerOptions Options
        {
            get { return _options; }
        }

        private static NodeRegistry CreateDefaultNodeRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return registry;
        }

        private void InitializeResources()
        {
            Resources["FlowPageBackground"] = BrushFromRgb(246, 248, 252);
            Resources["FlowPanelBackground"] = Brushes.White;
            Resources["FlowPanelBorder"] = BrushFromRgb(222, 229, 238);
            Resources["FlowAccent"] = BrushFromRgb(22, 101, 52);
            Resources["FlowText"] = BrushFromRgb(17, 24, 39);
            Resources["FlowMutedText"] = BrushFromRgb(100, 116, 139);
            InstallScrollBarResources();
        }

        private void InstallScrollBarResources()
        {
            var dictionary = (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Style x:Key=""FlowScrollBarPageButton"" TargetType=""{x:Type RepeatButton}"">
        <Setter Property=""Focusable"" Value=""False"" />
        <Setter Property=""OverridesDefaultStyle"" Value=""True"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type RepeatButton}"">
                    <Border Background=""Transparent"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <Style x:Key=""FlowScrollBarThumb"" TargetType=""{x:Type Thumb}"">
        <Setter Property=""Focusable"" Value=""False"" />
        <Setter Property=""Background"" Value=""#94A3B8"" />
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type Thumb}"">
                    <Border Margin=""2""
                            Background=""{TemplateBinding Background}""
                            CornerRadius=""4"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
                <Setter Property=""Background"" Value=""#64748B"" />
            </Trigger>
            <Trigger Property=""IsDragging"" Value=""True"">
                <Setter Property=""Background"" Value=""#166534"" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <ControlTemplate x:Key=""FlowVerticalScrollBarTemplate"" TargetType=""{x:Type ScrollBar}"">
        <Border Width=""10""
                Background=""{TemplateBinding Background}""
                CornerRadius=""5""
                SnapsToDevicePixels=""True"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageUpCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb MinHeight=""28"" Style=""{StaticResource FlowScrollBarThumb}"" />
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageDownCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.IncreaseRepeatButton>
            </Track>
        </Border>
    </ControlTemplate>

    <ControlTemplate x:Key=""FlowHorizontalScrollBarTemplate"" TargetType=""{x:Type ScrollBar}"">
        <Border Height=""10""
                Background=""{TemplateBinding Background}""
                CornerRadius=""5""
                SnapsToDevicePixels=""True"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""False"">
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageLeftCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.DecreaseRepeatButton>
                <Track.Thumb>
                    <Thumb MinWidth=""28"" Style=""{StaticResource FlowScrollBarThumb}"" />
                </Track.Thumb>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command=""ScrollBar.PageRightCommand""
                                  Style=""{StaticResource FlowScrollBarPageButton}"" />
                </Track.IncreaseRepeatButton>
            </Track>
        </Border>
    </ControlTemplate>

    <Style TargetType=""{x:Type ScrollBar}"">
        <Setter Property=""Background"" Value=""#E2E8F0"" />
        <Setter Property=""Width"" Value=""10"" />
        <Setter Property=""MinWidth"" Value=""10"" />
        <Setter Property=""Template"" Value=""{StaticResource FlowVerticalScrollBarTemplate}"" />
        <Style.Triggers>
            <Trigger Property=""Orientation"" Value=""Horizontal"">
                <Setter Property=""Width"" Value=""Auto"" />
                <Setter Property=""MinWidth"" Value=""32"" />
                <Setter Property=""Height"" Value=""10"" />
                <Setter Property=""MinHeight"" Value=""10"" />
                <Setter Property=""Template"" Value=""{StaticResource FlowHorizontalScrollBarTemplate}"" />
            </Trigger>
        </Style.Triggers>
    </Style>
</ResourceDictionary>");

            foreach (var key in dictionary.Keys)
            {
                Resources[key] = dictionary[key];
            }
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
            border.Background = BrushFromRgb(248, 250, 252);
            border.BorderBrush = BrushFromRgb(232, 238, 246);
            border.CornerRadius = new CornerRadius(8);

            _canvasScale = new ScaleTransform(1.0, 1.0);
            _surface = new Grid
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                Background = BrushFromRgb(248, 250, 252),
                Cursor = Cursors.Hand
            };
            _surface.LayoutTransform = _canvasScale;
            _surface.Children.Add(CreateGridLayer());
            _surface.Children.Add(_edges);
            _surface.Children.Add(_nodeLayer);
            _surface.PreviewMouseWheel += OnCanvasMouseWheel;
            _surface.MouseDown += OnSurfaceMouseDown;
            _surface.MouseMove += OnSurfaceMouseMove;
            _surface.MouseUp += OnSurfaceMouseUp;

            _canvasScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = _surface
            };
            _canvasScroll.ScrollChanged += delegate { SaveCanvasViewState(); };

            var canvasRoot = new Grid
            {
                ClipToBounds = true
            };
            canvasRoot.PreviewMouseWheel += OnCanvasMouseWheel;
            canvasRoot.Children.Add(_canvasScroll);
            canvasRoot.Children.Add(CreateZoomOverlay());
            border.Child = canvasRoot;

            return border;
        }

        private UIElement CreateZoomOverlay()
        {
            var overlay = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 14, 14),
                Padding = new Thickness(4, 2, 4, 2),
                CornerRadius = new CornerRadius(7),
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = 0.12,
                    Color = Color.FromRgb(15, 23, 42)
                }
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            overlay.Child = row;

            row.Children.Add(CreateZoomButton("-", delegate { ChangeCanvasZoom(0.9); }));
            _zoomText = new TextBlock
            {
                Width = 42,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Medium,
                FontSize = 12,
                Foreground = BrushFromRgb(100, 116, 139)
            };
            row.Children.Add(_zoomText);
            row.Children.Add(CreateZoomButton("+", delegate { ChangeCanvasZoom(1.1); }));
            UpdateZoomText();

            return overlay;
        }

        private static Button CreateZoomButton(string text, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = CreateZoomIcon(text),
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = BrushFromRgb(71, 85, 105),
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                Focusable = false,
                Template = CreateZoomButtonTemplate()
            };
            button.MouseEnter += delegate { button.Background = BrushFromRgb(241, 245, 249); };
            button.MouseLeave += delegate { button.Background = Brushes.Transparent; };
            button.Click += handler;
            return button;
        }

        private static ControlTemplate CreateZoomButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;
            return template;
        }

        private static UIElement CreateZoomIcon(string text)
        {
            var canvas = new Canvas
            {
                Width = 16,
                Height = 16,
                IsHitTestVisible = false
            };

            var stroke = BrushFromRgb(71, 85, 105);
            var circle = new Ellipse
            {
                Width = 8,
                Height = 8,
                Stroke = stroke,
                StrokeThickness = 1.3
            };
            Canvas.SetLeft(circle, 2);
            Canvas.SetTop(circle, 2);
            canvas.Children.Add(circle);

            var mark = new TextBlock
            {
                Text = text,
                Width = 8,
                Height = 8,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = stroke,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(mark, 2);
            Canvas.SetTop(mark, 1);
            canvas.Children.Add(mark);

            var handle = new Line
            {
                X1 = 9,
                Y1 = 9,
                X2 = 13,
                Y2 = 13,
                Stroke = stroke,
                StrokeThickness = 1.3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            canvas.Children.Add(handle);

            return canvas;
        }

        private UIElement CreateGridLayer()
        {
            return new Rectangle
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                Fill = CreateDotGridBrush(),
                IsHitTestVisible = false
            };
        }

        private static Brush CreateDotGridBrush()
        {
            var drawing = new GeometryDrawing
            {
                Brush = BrushFromRgb(226, 232, 240),
                Geometry = new EllipseGeometry(new Point(1.2, 1.2), 0.65, 0.65)
            };

            var brush = new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewbox = new Rect(0, 0, 14, 14),
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 14, 14),
                ViewportUnits = BrushMappingMode.Absolute
            };
            brush.Freeze();
            return brush;
        }

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

        private void CreateNewDesign()
        {
            _document = CreateDocument("designer-flow", "Designer Flow");
            _selectedNode = null;
            _selectedEdge = null;
            RenderCanvas();
            ApplyCanvasViewState();
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
            _selectedEdge = null;
            RenderCanvas();
            ApplyCanvasViewState();
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
            AddEdge(fromNodeId, "Next", toNodeId, "In");
        }

        private void AddEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
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
                FromPort = string.IsNullOrWhiteSpace(fromPort) ? "Next" : fromPort,
                ToNodeId = toNodeId,
                ToPort = string.IsNullOrWhiteSpace(toPort) ? "In" : toPort
            });
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
                card.MouseLeftButtonDown += OnNodeMouseDown;
                card.MouseMove += OnNodeMouseMove;
                card.MouseLeftButtonUp += OnNodeMouseUp;
                card.OutputPortDragStarted += OnOutputPortDragStarted;
                card.InputPortDragCompleted += OnInputPortDragCompleted;
                card.ContextMenu = CreateNodeContextMenu(node);
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
            AddVariableSuggestion(items, "token", "CaptureGroupId");
            AddVariableSuggestion(items, "token", "ScanGroupId");
            AddVariableSuggestion(items, "token", "TriggerId");
            AddVariableSuggestion(items, "token", "FrameId");
            AddVariableSuggestion(items, "token", "ShotIndex");
            AddVariableSuggestion(items, "token", "FrameIndex");
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
            if (node == null)
            {
                return;
            }

            node.Settings["Disabled"] = !IsNodeDisabled(node);
            RenderCanvas();
            SelectNode(node);
        }

        private static bool IsNodeDisabled(NodeDefinition node)
        {
            object value;
            return node != null &&
                node.Settings != null &&
                node.Settings.TryGetValue("Disabled", out value) &&
                value != null &&
                Convert.ToBoolean(value, CultureInfo.InvariantCulture);
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
                _selectedEdge = null;
                RenderCanvas();
                ApplyCanvasViewState();
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
                SaveCanvasViewState();
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
                var publishResult = new FlowPublishService(_nodeRegistry).Publish(_document);
                if (!publishResult.IsSuccess)
                {
                    AddDebugMessage("Publish validation failed: " + FormatValidationIssues(publishResult.Validation));
                    return;
                }

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dialog.FileName));
                RuntimeFlowSerializer.Save(dialog.FileName, publishResult.Runtime);
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
            _nodeStartTimes.Clear();
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
                if (runtimeEvent.EventType == FlowRuntimeEventType.NodeStarted)
                {
                    _nodeStartTimes[runtimeEvent.NodeId] = DateTime.UtcNow;
                }

                TimeSpan? elapsed = null;
                DateTime started;
                if ((runtimeEvent.EventType == FlowRuntimeEventType.NodeCompleted ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeFailed ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeTimeout) &&
                    _nodeStartTimes.TryGetValue(runtimeEvent.NodeId, out started))
                {
                    elapsed = DateTime.UtcNow - started;
                }

                _nodeCards[runtimeEvent.NodeId].SetRuntimeState(runtimeEvent.State, elapsed, runtimeEvent.Message);
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
            var selected = _selectedNode != null
                ? _selectedNode.Id
                : (_selectedEdge == null ? "none" : FormatEdgeLabel(_selectedEdge));
            var zoom = _canvasScale == null ? 1.0 : _canvasScale.ScaleX;
            _statusText.Text = string.Format(CultureInfo.InvariantCulture, "{0} nodes | {1} edges | zoom {2:P0} | selected: {3}", nodeCount, edgeCount, zoom, selected);
        }

        private static string FormatValidationIssues(FlowValidationResult validation)
        {
            if (validation == null || validation.Issues.Count == 0)
            {
                return "unknown validation failure.";
            }

            var parts = validation.Issues
                .Where(x => x.Severity == FlowValidationSeverity.Error)
                .Take(4)
                .Select(x => string.IsNullOrWhiteSpace(x.NodeId)
                    ? x.Code + ": " + x.Message
                    : x.Code + " [" + x.NodeId + "]: " + x.Message)
                .ToArray();
            return string.Join("; ", parts);
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

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        internal static string CreatePortAnchorKey(string nodeId, string direction, string portName)
        {
            return (nodeId ?? string.Empty) + "|" + (direction ?? string.Empty) + "|" + (portName ?? string.Empty);
        }

        internal static bool EdgeEquals(EdgeDefinition left, EdgeDefinition right)
        {
            return left != null &&
                right != null &&
                StringEquals(left.FromNodeId, right.FromNodeId) &&
                StringEquals(left.FromPort, right.FromPort) &&
                StringEquals(left.ToNodeId, right.ToNodeId) &&
                StringEquals(left.ToPort, right.ToPort);
        }

        internal static string FormatEdgeLabel(EdgeDefinition edge)
        {
            if (edge == null)
            {
                return "none";
            }

            return (edge.FromNodeId ?? string.Empty) + "." + (string.IsNullOrWhiteSpace(edge.FromPort) ? "?" : edge.FromPort) +
                " -> " +
                (edge.ToNodeId ?? string.Empty) + "." + (string.IsNullOrWhiteSpace(edge.ToPort) ? "?" : edge.ToPort);
        }

        private static bool IsTextEditorFocused()
        {
            var current = Keyboard.FocusedElement as DependencyObject;
            while (current != null)
            {
                if (current is TextBox || current is PasswordBox || current is ComboBox)
                {
                    return true;
                }

                DependencyObject parent = null;
                try
                {
                    parent = VisualTreeHelper.GetParent(current);
                }
                catch (InvalidOperationException)
                {
                    parent = null;
                }

                if (parent == null)
                {
                    parent = LogicalTreeHelper.GetParent(current);
                }

                current = parent;
            }

            return false;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                var typed = current as T;
                if (typed != null)
                {
                    return typed;
                }

                DependencyObject parent = null;
                try
                {
                    parent = VisualTreeHelper.GetParent(current);
                }
                catch (InvalidOperationException)
                {
                    parent = null;
                }

                if (parent == null)
                {
                    parent = LogicalTreeHelper.GetParent(current);
                }

                current = parent;
            }

            return null;
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
        private readonly StackPanel _summaryRows;
        private readonly Border _stateChip;
        private readonly TextBlock _stateText;
        private bool _isDisabled;

        public NodeCardControl(NodeViewModel viewModel)
        {
            ViewModel = viewModel;
            Width = 190;
            MinHeight = 86;
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(52, 211, 153);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            Padding = new Thickness(9, 8, 9, 8);
            Cursor = Cursors.SizeAll;
            InputPortControls = new List<PortControl>();
            OutputPortControls = new List<PortControl>();
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Opacity = 0.08,
                Color = Color.FromRgb(15, 23, 42)
            };

            var chrome = new Grid();
            Child = chrome;

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
            BorderBrush = isSelected
                ? FlowDesignerControl.BrushFromRgb(16, 185, 129)
                : FlowDesignerControl.BrushFromRgb(52, 211, 153);
            BorderThickness = isSelected ? new Thickness(1.6) : new Thickness(1);
        }

        public void SetDisabled(bool isDisabled)
        {
            _isDisabled = isDisabled;
            Opacity = isDisabled ? 0.58 : 1.0;
            if (isDisabled)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(226, 232, 240);
                _stateChip.ToolTip = "Disabled";
                _stateText.Text = string.Empty;
            }
        }

        public void SetRuntimeState(NodeRuntimeState state)
        {
            SetRuntimeState(state, null, null);
        }

        public void SetRuntimeState(NodeRuntimeState state, TimeSpan? elapsed, string message)
        {
            if (_isDisabled && state == NodeRuntimeState.Waiting)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(226, 232, 240);
                _stateChip.ToolTip = "Disabled";
                _stateText.Text = string.Empty;
                ToolTip = "Node is disabled in the designer.";
                return;
            }

            ToolTip = string.IsNullOrWhiteSpace(message) ? null : message;
            if (state == NodeRuntimeState.Running)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(245, 158, 11);
                _stateChip.ToolTip = "Running";
                _stateText.Text = string.Empty;
                return;
            }

            if (state == NodeRuntimeState.Completed)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(16, 185, 129);
                _stateChip.ToolTip = elapsed.HasValue ? "Done " + FormatElapsed(elapsed.Value) : "Done";
                _stateText.Text = string.Empty;
                return;
            }

            if (state == NodeRuntimeState.Failed)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(239, 68, 68);
                _stateChip.ToolTip = elapsed.HasValue ? "Failed " + FormatElapsed(elapsed.Value) : "Failed";
                _stateText.Text = string.Empty;
                return;
            }

            if (state == NodeRuntimeState.Timeout)
            {
                _stateChip.Background = FlowDesignerControl.BrushFromRgb(249, 115, 22);
                _stateChip.ToolTip = elapsed.HasValue ? "Timeout " + FormatElapsed(elapsed.Value) : "Timeout";
                _stateText.Text = string.Empty;
                return;
            }

            _stateChip.Background = FlowDesignerControl.BrushFromRgb(16, 185, 129);
            _stateChip.ToolTip = "Ready";
            _stateText.Text = string.Empty;
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

        private static Brush GetNodeAccentBrush(string nodeType)
        {
            var type = nodeType ?? string.Empty;
            if (type.StartsWith("camera.", StringComparison.OrdinalIgnoreCase))
            {
                return FlowDesignerControl.BrushFromRgb(59, 130, 246);
            }

            if (type.StartsWith("light.", StringComparison.OrdinalIgnoreCase))
            {
                return FlowDesignerControl.BrushFromRgb(245, 158, 11);
            }

            if (type.StartsWith("database.", StringComparison.OrdinalIgnoreCase))
            {
                return FlowDesignerControl.BrushFromRgb(20, 184, 166);
            }

            if (type.StartsWith("join.", StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith("group.", StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith("scan.", StringComparison.OrdinalIgnoreCase) ||
                type.StartsWith("fusion.", StringComparison.OrdinalIgnoreCase))
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

            if (type.StartsWith("camera.", StringComparison.OrdinalIgnoreCase))
            {
                return "CAM";
            }

            if (type.StartsWith("database.", StringComparison.OrdinalIgnoreCase))
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

        public PortControl(PortViewModel port)
        {
            Port = port;
            var isInput = port != null && string.Equals(port.Direction, "Input", StringComparison.OrdinalIgnoreCase);
            _normalFill = FlowDesignerControl.BrushFromRgb(59, 130, 246);
            _hoverFill = FlowDesignerControl.BrushFromRgb(37, 99, 235);

            Width = 14;
            Height = 22;
            CornerRadius = new CornerRadius(0);
            Margin = new Thickness(0, 4, 0, 4);
            Padding = new Thickness(0);
            Background = Brushes.Transparent;
            BorderThickness = new Thickness(0);
            Cursor = isInput ? Cursors.Cross : Cursors.Hand;
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

    public sealed class EdgeLayerControl : Canvas
    {
        private const double NodeCardWidth = 190;
        private const double PortAnchorY = 43;

        private bool _hasPreview;
        private Point _previewStart;
        private Point _previewEnd;

        public EdgeLayerControl()
        {
            Width = 1800;
            Height = 1100;
            IsHitTestVisible = true;
        }

        public event Action<EdgeDefinition> EdgeSelected;

        public event Action<EdgeDefinition> EdgeDeleteRequested;

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

    public sealed class PropertyPanelControl : Border
    {
        private readonly StackPanel _rows;
        private NodeDefinition _node;
        private Action _changed;
        private IList<string> _variableExpressions;

        public PropertyPanelControl()
        {
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            _rows = new StackPanel();
            _variableExpressions = new List<string>();
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _rows
            };
        }

        public void ShowNode(NodeDefinition node, NodeDescriptor descriptor, Action changed)
        {
            ShowNode(node, descriptor, null, changed);
        }

        public void ShowNode(NodeDefinition node, NodeDescriptor descriptor, IEnumerable<string> variableExpressions, Action changed)
        {
            _node = node;
            _changed = changed;
            _variableExpressions = variableExpressions == null
                ? new List<string>()
                : variableExpressions
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
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
                    AddSettingField(setting, value, delegate(object newValue)
                    {
                        node.Settings[setting.Name] = newValue;
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
                    AddBindingField(input.DisplayName + " (" + input.Name + ")", text, delegate(string value)
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

        private void AddSettingField(NodeSettingDescriptor setting, object value, Action<object> setter)
        {
            var label = setting.DisplayName + " (" + setting.Name + ")";
            if (string.Equals(setting.DataType, "Boolean", StringComparison.OrdinalIgnoreCase))
            {
                _rows.Children.Add(CreateLabel(label));
                var checkBox = new CheckBox
                {
                    IsChecked = value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                checkBox.Checked += delegate { ApplySetting(setter, true); };
                checkBox.Unchecked += delegate { ApplySetting(setter, false); };
                _rows.Children.Add(checkBox);
                return;
            }

            if (IsBindingSetting(setting))
            {
                AddBindingField(label, ToEditorText(setting, value), delegate(string text)
                {
                    ApplySetting(setter, ConvertFromEditorText(setting, text));
                });
                return;
            }

            var selectorItems = GetSelectorItems(setting);
            if (selectorItems.Count > 0)
            {
                _rows.Children.Add(CreateLabel(label));
                var comboBox = new ComboBox
                {
                    IsEditable = true,
                    Text = ToEditorText(setting, value),
                    MinHeight = 28,
                    Margin = new Thickness(0, 0, 0, 4),
                    BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
                };
                foreach (var item in selectorItems)
                {
                    comboBox.Items.Add(item);
                }

                comboBox.LostFocus += delegate { ApplySetting(setter, ConvertFromEditorText(setting, comboBox.Text)); };
                comboBox.DropDownClosed += delegate { ApplySetting(setter, ConvertFromEditorText(setting, comboBox.Text)); };
                _rows.Children.Add(comboBox);
                return;
            }

            AddTextField(label, ToEditorText(setting, value), true, delegate(string text)
            {
                ApplySetting(setter, ConvertFromEditorText(setting, text));
            });
        }

        private void AddBindingField(string label, string value, Action<string> setter)
        {
            _rows.Children.Add(CreateLabel(label));
            var dock = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 4)
            };
            var selector = new VariableSelectorControl(_variableExpressions);
            DockPanel.SetDock(selector, Dock.Right);
            dock.Children.Add(selector);

            var textBox = new TextBox
            {
                Text = value ?? string.Empty,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            selector.VariableSelected += delegate(string expression)
            {
                textBox.Text = string.IsNullOrWhiteSpace(textBox.Text) ? expression : textBox.Text + " " + expression;
                setter(textBox.Text);
                RaiseChanged();
            };
            textBox.LostFocus += delegate
            {
                setter(textBox.Text);
                RaiseChanged();
            };
            dock.Children.Add(textBox);
            _rows.Children.Add(dock);
        }

        private static bool IsBindingSetting(NodeSettingDescriptor setting)
        {
            if (setting == null || string.IsNullOrWhiteSpace(setting.Name))
            {
                return false;
            }

            if (string.Equals(setting.Name, "FieldMappings", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return setting.Name.EndsWith("Binding", StringComparison.OrdinalIgnoreCase) ||
                setting.Name.EndsWith("Bindings", StringComparison.OrdinalIgnoreCase);
        }

        private void AddTextField(string label, string value, bool editable, Action<string> setter)
        {
            _rows.Children.Add(CreateLabel(label));

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
                    RaiseChanged();
                }
            };
            _rows.Children.Add(textBox);
        }

        private static TextBlock CreateLabel(string label)
        {
            return new TextBlock
            {
                Text = label,
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 8, 0, 3)
            };
        }

        private void ApplySetting(Action<object> setter, object value)
        {
            if (setter != null)
            {
                setter(value);
                RaiseChanged();
            }
        }

        private void RaiseChanged()
        {
            if (_changed != null)
            {
                _changed();
            }
        }

        private static IList<string> GetSelectorItems(NodeSettingDescriptor setting)
        {
            var items = new List<string>();
            if (setting == null)
            {
                return items;
            }

            if (string.Equals(setting.Name, "CameraId", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("Camera01");
            }
            else if (string.Equals(setting.Name, "LightId", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("Light01");
            }
            else if (string.Equals(setting.Name, "RecipeId", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("Recipe01");
            }
            else if (string.Equals(setting.Name, "DatabaseId", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("VisionDb");
            }
            else if (string.Equals(setting.Name, "SaverId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting.Name, "ImageSaverId", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("ImageSave01");
            }
            else if (string.Equals(setting.Name, "MatchMode", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("TriggerId");
                items.Add("Any");
                items.Add("ScanGroupId");
            }
            else if (string.Equals(setting.Name, "CallbackMode", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("WaitNextFrame");
                items.Add("StreamFrames");
            }
            else if (string.Equals(setting.Name, "DuplicatePolicy", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("Error");
                items.Add("Ignore");
                items.Add("Replace");
            }
            else if (string.Equals(setting.Name, "QueueFullMode", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("Wait");
                items.Add("Reject");
            }
            else if (string.Equals(setting.Name, "QueueName", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("recipe");
                items.Add("image-save");
                items.Add("database-save");
            }

            return items;
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

    public sealed class VariableSelectorControl : Button
    {
        private readonly IList<string> _variables;

        public VariableSelectorControl()
            : this(null)
        {
        }

        public VariableSelectorControl(IEnumerable<string> variables)
        {
            _variables = variables == null
                ? new List<string>()
                : variables
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            Content = "Var";
            MinWidth = 44;
            Height = 28;
            Margin = new Thickness(6, 0, 0, 0);
            ToolTip = "Insert a variable binding.";
            Click += OnClick;
        }

        public event Action<string> VariableSelected;

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (_variables.Count == 0)
            {
                RaiseVariableSelected("{{ node.Output }}");
                return;
            }

            var menu = new ContextMenu
            {
                PlacementTarget = this
            };
            foreach (var variable in _variables)
            {
                var item = new MenuItem
                {
                    Header = variable
                };
                item.Click += delegate { RaiseVariableSelected(variable); };
                menu.Items.Add(item);
            }

            ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void RaiseVariableSelected(string expression)
        {
            var handler = VariableSelected;
            if (handler != null)
            {
                handler(expression);
            }
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
            _events.SelectionChanged += OnSelectionChanged;
            layout.Children.Add(_events);
        }

        public event Action<string> NodeRequested;

        public void Clear()
        {
            _events.Items.Clear();
        }

        public void AddMessage(string message)
        {
            _events.Items.Add(new ListBoxItem
            {
                Content = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message
            });
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
            _events.Items.Add(new ListBoxItem
            {
                Content = text,
                Tag = runtimeEvent.NodeId
            });
            _events.ScrollIntoView(_events.Items[_events.Items.Count - 1]);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _events.SelectedItem as ListBoxItem;
            var nodeId = item == null ? null : item.Tag as string;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var handler = NodeRequested;
            if (handler != null)
            {
                handler(nodeId);
            }
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
