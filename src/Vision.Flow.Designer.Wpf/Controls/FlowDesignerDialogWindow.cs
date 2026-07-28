using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Vision.Flow.Designer.Wpf.Theming;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// SDK 内部确认和编辑弹窗的统一无边框外壳。
    /// </summary>
    internal sealed class FlowDesignerDialogWindow : Window
    {
        private readonly ContentControl _contentHost;

        public FlowDesignerDialogWindow(string title, double width, double height, Window owner)
        {
            Title = title ?? string.Empty;
            Width = width;
            Height = height;
            MinWidth = Math.Min(width, 400);
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner;
            Owner = owner;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            FlowDesignerTheme.ApplyTo(this);

            var shadowFrame = new Border
            {
                Margin = new Thickness(12),
                Background = Brushes.White,
                BorderBrush = FlowDesignerControl.BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(15, 23, 42),
                    BlurRadius = 20,
                    ShadowDepth = 4,
                    Opacity = 0.24
                }
            };

            var shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shadowFrame.Child = shell;

            var titleBar = CreateTitleBar(title);
            titleBar.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    DragMove();
                }
            };
            shell.Children.Add(titleBar);

            _contentHost = new ContentControl
            {
                Background = Brushes.White
            };
            Grid.SetRow(_contentHost, 1);
            shell.Children.Add(_contentHost);
            base.Content = shadowFrame;

            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    e.Handled = true;
                }
            };
        }

        public object DialogContent
        {
            get { return _contentHost.Content; }
            set { _contentHost.Content = value; }
        }

        private UIElement CreateTitleBar(string title)
        {
            var bar = new Grid
            {
                Background = FlowDesignerControl.BrushFromRgb(24, 50, 75)
            };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            bar.Children.Add(new TextBlock
            {
                Text = title ?? string.Empty,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            });

            var closeIcon = new Path
            {
                Data = Geometry.Parse("M4,4 L14,14 M14,4 L4,14"),
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            var close = new Button
            {
                Content = closeIcon,
                Width = 44,
                Height = 44,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "关闭",
                Tag = "DialogClose"
            };
            close.Click += delegate { DialogResult = false; };
            close.MouseEnter += delegate { close.Background = FlowDesignerControl.BrushFromRgb(190, 50, 62); };
            close.MouseLeave += delegate { close.Background = Brushes.Transparent; };
            Grid.SetColumn(close, 1);
            bar.Children.Add(close);
            return bar;
        }
    }
}
