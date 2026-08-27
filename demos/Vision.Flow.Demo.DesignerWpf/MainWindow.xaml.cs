using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.Theming;

namespace Vision.Flow.Demo.DesignerWpf
{
    public partial class MainWindow : Window
    {
        private readonly FlowDesignerControl _designer;

        public MainWindow()
        {
            InitializeComponent();
            FlowDesignerTheme.ApplyTo(this);
            _designer = new FlowDesignerControl(
                null,
                new FlowDesignerOptions
                {
                    LoadSampleOnStartup = true
                });
            DesignerHost.Children.Add(_designer);
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                ToggleMaximized();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnMaximizeClick(object sender, RoutedEventArgs e)
        {
            ToggleMaximized();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleMaximized()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void OnWindowStateChanged(object sender, System.EventArgs e)
        {
            if (MaximizeButton != null)
            {
                MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "还原" : "最大化";
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_designer != null && !_designer.TryResolvePendingPropertyChanges())
            {
                e.Cancel = true;
            }
        }

        private static T FindAncestor<T>(DependencyObject source)
            where T : DependencyObject
        {
            var current = source;
            while (current != null)
            {
                var typed = current as T;
                if (typed != null)
                {
                    return typed;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
