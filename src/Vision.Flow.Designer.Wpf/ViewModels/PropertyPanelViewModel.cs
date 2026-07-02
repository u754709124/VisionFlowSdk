using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
    /// <summary>
    /// 属性面板视图模型，记录当前被编辑的节点。
    /// </summary>
    public sealed class PropertyPanelViewModel
    {
        public NodeDefinition SelectedNode { get; set; }
    }
}
