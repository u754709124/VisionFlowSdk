using System.Windows;
using Vision.Flow.Designer.Wpf.Controls;
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
using Vision.Flow.Designer.Wpf.ViewModels;

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

