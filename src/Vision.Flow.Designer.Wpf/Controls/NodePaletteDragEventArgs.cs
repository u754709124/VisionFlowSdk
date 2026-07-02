using System;
using System.Windows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 节点库拖拽事件只�?Designer 内部使用，用于把节点类型从左侧节点库带到画布�?
    public sealed class NodePaletteDragEventArgs : EventArgs
    {
        public NodePaletteDragEventArgs(NodeDescriptor descriptor, UIElement dragSource)
        {
            Descriptor = descriptor;
            DragSource = dragSource;
        }

        public NodeDescriptor Descriptor { get; private set; }

        public UIElement DragSource { get; private set; }
    }
}
