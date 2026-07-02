using System.Collections.Generic;
using Vision.Flow.Core.Descriptors;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
    /// <summary>
    /// 节点库视图模型，提供可添加节点的描述集合。
    /// </summary>
    public sealed class NodePaletteViewModel
    {
        public IList<NodeDescriptor> Nodes { get; set; }
    }
}
