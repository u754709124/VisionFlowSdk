using System.Windows;
using Vision.Flow.Designer.Wpf;

namespace Vision.Flow.Demo.DesignerWpf
{
    public partial class MainWindow : Window
    {
        private readonly FlowDesignerControl _designer;

        public MainWindow()
        {
            InitializeComponent();
            _designer = new FlowDesignerControl(
                null,
                null,
                new FlowDesignerOptions
                {
                    LoadSampleOnStartup = true
                });
            DesignerHost.Children.Add(_designer);
        }
    }
}
