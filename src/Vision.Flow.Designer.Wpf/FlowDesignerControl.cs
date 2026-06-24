using System.Windows;
using System.Windows.Controls;

namespace Vision.Flow.Designer.Wpf
{
    public sealed class FlowDesignerControl : UserControl
    {
        public FlowDesignerControl()
        {
            Content = new TextBlock
            {
                Text = "Vision Flow Designer",
                Margin = new Thickness(16)
            };
        }
    }
}
