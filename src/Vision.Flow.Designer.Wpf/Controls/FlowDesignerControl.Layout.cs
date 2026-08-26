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
using System.Windows.Data;
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
using Vision.Flow.Designer.Wpf.Theming;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
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
            var workspaceRow = 0;
            if (_options.ToolbarPlacement == FlowDesignerToolbarPlacement.Internal)
            {
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
                Grid.SetRow(_toolbarView, 0);
                root.Children.Add(_toolbarView);
                workspaceRow = 1;
            }

            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _debugRowDefinition = new RowDefinition { Height = new GridLength(36) };
            root.RowDefinitions.Add(_debugRowDefinition);

            var workspace = new Grid
            {
                Margin = new Thickness(0)
            };
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(244) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
            Grid.SetRow(workspace, workspaceRow);

            Grid.SetColumn(_palette, 0);
            workspace.Children.Add(_palette);

            var canvasPanel = CreateCanvasPanel();
            Grid.SetColumn(canvasPanel, 1);
            workspace.Children.Add(canvasPanel);

            var rightPanel = new Grid
            {
                Background = Brushes.White
            };
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(_entryTriggerPanel, 0);
            rightPanel.Children.Add(_entryTriggerPanel);
            Grid.SetRow(_properties, 1);
            rightPanel.Children.Add(_properties);
            Grid.SetColumn(rightPanel, 2);
            workspace.Children.Add(rightPanel);
            root.Children.Add(workspace);

            Grid.SetRow(_debug, workspaceRow + 1);
            root.Children.Add(_debug);

            return root;
        }

        private FrameworkElement CreateToolbar()
        {
            var root = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = _options.ToolbarPlacement == FlowDesignerToolbarPlacement.External
                    ? new Thickness(0)
                    : new Thickness(12, 7, 12, 7)
            };
            FlowDesignerTheme.ApplyTo(root);

            var dock = new DockPanel();
            root.Child = dock;

            var statusBorder = new Border
            {
                Background = BrushFromRgb(245, 247, 250),
                BorderBrush = BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 0, 10, 0),
                Height = 34,
                MaxWidth = 370,
                Child = _statusText
            };
            if (_options.ToolbarPlacement == FlowDesignerToolbarPlacement.Internal)
            {
                DockPanel.SetDock(statusBorder, Dock.Right);
                dock.Children.Add(statusBorder);
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            dock.Children.Add(buttons);

            _editModeButton = CreateToolbarButton("编辑模式", "edit", async delegate { await SetInteractionModeAsync(DesignerInteractionMode.Edit); });
            _debugModeButton = CreateToolbarButton("调试运行", "debug", async delegate { await SetInteractionModeAsync(DesignerInteractionMode.DebugRun); });
            buttons.Children.Add(_editModeButton);
            buttons.Children.Add(_debugModeButton);
            buttons.Children.Add(CreateToolbarSpacer(_options.ToolbarPlacement == FlowDesignerToolbarPlacement.External));

            _newButton = CreateToolbarButton("New", "new", delegate { CreateNewDesign(); });
            _sampleButton = CreateToolbarButton("Sample", "sample", delegate { LoadCoreBasicTemplate(); });
            _openButton = CreateToolbarButton("Open", "open", delegate { OpenDesign(); });
            _saveButton = CreateToolbarButton("Save", "save", delegate { SaveDesign(); });
            _publishButton = CreateToolbarButton("Publish", "publish", delegate { ShowPublishRuntimeDialog(); });
            _entryListButton = CreateToolbarButton("入口列表", "entries", delegate { ShowEntryListDialog(); }, true);
            _debugRunButton = CreateToolbarButton("运行", "run", async delegate { await RunDebugAsync(); });
            _stopButton = CreateToolbarButton("停止", "stop", async delegate { await StopDebugAsync(); });

            if (_options.ShowStandaloneDocumentCommands)
            {
                buttons.Children.Add(_newButton);
                buttons.Children.Add(_sampleButton);
                buttons.Children.Add(_openButton);
                buttons.Children.Add(_saveButton);
                buttons.Children.Add(_publishButton);
            }

            buttons.Children.Add(_entryListButton);
            buttons.Children.Add(_debugRunButton);
            buttons.Children.Add(_stopButton);

            return root;
        }

        private UIElement CreateCanvasPanel()
        {
            var border = CreatePanelBorder(new Thickness(8, 8, 8, 8));
            border.Padding = new Thickness(0);
            border.Background = BrushFromRgb(248, 250, 252);
            border.BorderBrush = BrushFromRgb(232, 238, 246);
            border.CornerRadius = new CornerRadius(6);

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
            TextOptions.SetTextFormattingMode(_surface, TextFormattingMode.Ideal);
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
            _canvasScroll.ScrollChanged += delegate
            {
                SaveCanvasViewState();
                UpdateMiniMap();
            };

            var canvasRoot = new Grid
            {
                ClipToBounds = true
            };
            canvasRoot.PreviewMouseWheel += OnCanvasMouseWheel;
            canvasRoot.Children.Add(_canvasScroll);
            canvasRoot.Children.Add(CreateMiniMapOverlay());
            canvasRoot.Children.Add(CreateZoomOverlay());
            border.Child = canvasRoot;

            return border;
        }

        private UIElement CreateMiniMapOverlay()
        {
            _miniMap.Width = 196;
            _miniMap.Height = 132;
            _miniMap.HorizontalAlignment = HorizontalAlignment.Left;
            _miniMap.VerticalAlignment = VerticalAlignment.Bottom;
            _miniMap.Margin = new Thickness(14, 0, 0, 14);
            _miniMap.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 1,
                Opacity = 0.14,
                Color = Color.FromRgb(15, 23, 42)
            };
            Panel.SetZIndex(_miniMap, 2);
            return _miniMap;
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
            var geometry = new GeometryGroup();
            geometry.Children.Add(new EllipseGeometry(new Point(6, 6), 4, 4));
            geometry.Children.Add(new LineGeometry(new Point(9, 9), new Point(14, 14)));
            geometry.Children.Add(new LineGeometry(new Point(4, 6), new Point(8, 6)));
            if (string.Equals(text, "+", StringComparison.Ordinal))
            {
                geometry.Children.Add(new LineGeometry(new Point(6, 4), new Point(6, 8)));
            }
            var stroke = BrushFromRgb(71, 85, 105);
            return new ShapesPath
            {
                Width = 16,
                Height = 16,
                Data = geometry,
                Stroke = stroke,
                StrokeThickness = 1.3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };
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

        private Button CreateToolbarButton(
            string text,
            string iconName,
            RoutedEventHandler handler,
            bool useIconOnlyInExternalToolbar = false)
        {
            var isExternalIconOnly = useIconOnlyInExternalToolbar &&
                _options.ToolbarPlacement == FlowDesignerToolbarPlacement.External;
            var button = new Button
            {
                Tag = text,
                MinWidth = isExternalIconOnly
                    ? 28
                    : _options.ToolbarPlacement == FlowDesignerToolbarPlacement.External
                    ? (text.Length > 3 ? 62 : 44)
                    : (text.Length > 7 ? 96 : 72),
                Height = 34,
                Margin = _options.ToolbarPlacement == FlowDesignerToolbarPlacement.External
                    ? new Thickness(0, 0, 2, 0)
                    : new Thickness(0, 0, 6, 0),
                Padding = _options.ToolbarPlacement == FlowDesignerToolbarPlacement.External
                    ? new Thickness(3, 0, 3, 0)
                    : new Thickness(12, 0, 12, 0)
            };
            if (isExternalIconOnly)
            {
                var icon = FlowDesignerIcons.Create(iconName, BrushFromRgb(75, 91, 112), 13);
                icon.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { Source = button });
                button.Content = icon;
                button.ToolTip = text;
            }
            else
            {
                button.Content = CreateToolbarButtonContent(
                    text,
                    iconName,
                    button,
                    _options.ToolbarPlacement == FlowDesignerToolbarPlacement.External);
            }
            System.Windows.Automation.AutomationProperties.SetName(button, text);
            button.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.ToolbarButtonStyleKey);
            button.Click += handler;
            return button;
        }

        private static UIElement CreateToolbarButtonContent(
            string text,
            string iconName,
            Button owner,
            bool isCompact)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var icon = FlowDesignerIcons.Create(iconName, BrushFromRgb(75, 91, 112), isCompact ? 13 : 15);
            icon.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { Source = owner });
            if (string.Equals(iconName, "stop", StringComparison.OrdinalIgnoreCase))
            {
                icon.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = owner });
            }
            panel.Children.Add(icon);
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Margin = new Thickness(isCompact ? 4 : 6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            return panel;
        }

        private static UIElement CreateToolbarSpacer(bool isCompact)
        {
            return new Border
            {
                Width = 1,
                Height = 24,
                Margin = isCompact
                    ? new Thickness(2, 4, 5, 4)
                    : new Thickness(2, 4, 10, 4),
                Background = BrushFromRgb(221, 229, 239)
            };
        }
    }
}
