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
    // 布局辅助方法构建静态外壳、工具栏、画布宿主和通用界面框架。
    public sealed partial class FlowDesignerControl
    {
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

            _editModeButton = CreateToolbarButton("编辑", async delegate { await SetInteractionModeAsync(DesignerInteractionMode.Edit); });
            _debugModeButton = CreateToolbarButton("调试运行", async delegate { await SetInteractionModeAsync(DesignerInteractionMode.DebugRun); });
            buttons.Children.Add(_editModeButton);
            buttons.Children.Add(_debugModeButton);
            buttons.Children.Add(CreateToolbarSpacer());

            _newButton = CreateToolbarButton("New", delegate { CreateNewDesign(); });
            _sampleButton = CreateToolbarButton("Sample", delegate { LoadCoreBasicTemplate(); });
            _openButton = CreateToolbarButton("Open", delegate { OpenDesign(); });
            _saveButton = CreateToolbarButton("Save", delegate { SaveDesign(); });
            _publishButton = CreateToolbarButton("Publish", delegate { PublishRuntime(); });
            _debugRunButton = CreateToolbarButton("Debug Run", async delegate { await RunDebugAsync(); });
            _stopButton = CreateToolbarButton("Stop", async delegate { await StopDebugAsync(); });

            buttons.Children.Add(_newButton);
            buttons.Children.Add(_sampleButton);
            buttons.Children.Add(_openButton);
            buttons.Children.Add(_saveButton);
            buttons.Children.Add(_publishButton);
            buttons.Children.Add(_debugRunButton);
            buttons.Children.Add(_stopButton);

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
                Width = _canvasWidth,
                Height = _canvasHeight,
                Background = BrushFromRgb(248, 250, 252),
                Cursor = Cursors.Hand,
                AllowDrop = true,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };
            TextOptions.SetTextFormattingMode(_surface, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(_surface, TextRenderingMode.ClearType);
            _surface.LayoutTransform = _canvasScale;
            _gridLayer = CreateGridLayer();
            _surface.Children.Add(_gridLayer);
            _surface.Children.Add(_edges);
            _surface.Children.Add(_nodeLayer);
            _surface.PreviewMouseWheel += OnCanvasMouseWheel;
            _surface.MouseDown += OnSurfaceMouseDown;
            _surface.MouseMove += OnSurfaceMouseMove;
            _surface.MouseUp += OnSurfaceMouseUp;
            _surface.DragOver += OnPaletteNodeDragOver;
            _surface.Drop += OnPaletteNodeDrop;

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

        private Rectangle CreateGridLayer()
        {
            return new Rectangle
            {
                Width = _canvasWidth,
                Height = _canvasHeight,
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

        private static UIElement CreateToolbarSpacer()
        {
            return new Border
            {
                Width = 1,
                Height = 24,
                Margin = new Thickness(2, 4, 10, 4),
                Background = BrushFromRgb(51, 65, 85)
            };
        }
    }
}
