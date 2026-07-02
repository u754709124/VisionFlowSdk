using System.Collections.Generic;
using System.Linq;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
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
}
