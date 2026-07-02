using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
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

        public FlowPortDirection Direction { get; private set; }

        public FlowDataType DataType { get; private set; }
    }
}
