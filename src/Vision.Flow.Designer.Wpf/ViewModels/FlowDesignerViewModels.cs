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
    // 设计器 ViewModel 是供设计器控件共享的轻量状态载体。
    /// <summary>
    /// 设计器根视图模型，承载当前设计态流程文档。
    /// </summary>
    public sealed class FlowDesignerViewModel
    {
        public FlowDesignDocument Document { get; set; }
    }

    /// <summary>
    /// 节点卡片视图模型，组合运行态节点定义和节点描述信息。
    /// </summary>
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

    /// <summary>
    /// 端口视图模型，为画布控件提供端口名称、方向和数据类型。
    /// </summary>
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

    /// <summary>
    /// 连线视图模型，包装设计器当前渲染的运行态连线定义。
    /// </summary>
    public sealed class EdgeViewModel
    {
        public EdgeDefinition Edge { get; set; }
    }

    /// <summary>
    /// 属性面板视图模型，记录当前被编辑的节点。
    /// </summary>
    public sealed class PropertyPanelViewModel
    {
        public NodeDefinition SelectedNode { get; set; }
    }

    /// <summary>
    /// 节点库视图模型，提供可添加节点的描述集合。
    /// </summary>
    public sealed class NodePaletteViewModel
    {
        public IList<NodeDescriptor> Nodes { get; set; }
    }

    /// <summary>
    /// 运行调试视图模型，保存设计器调试期间接收到的运行事件。
    /// </summary>
    public sealed class RuntimeDebugViewModel
    {
        public RuntimeDebugViewModel()
        {
            Events = new List<FlowRuntimeEvent>();
        }

        public IList<FlowRuntimeEvent> Events { get; private set; }
    }
}
