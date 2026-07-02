using System;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 端口连线事件只携带被拖拽端口和对应控件，避免画布依赖鼠标事件细节。
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
}
