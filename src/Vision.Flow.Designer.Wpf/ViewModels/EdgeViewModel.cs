using Vision.Flow.Core.Definitions;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
    /// <summary>
    /// 连线视图模型，包装设计器当前渲染的运行态连线定义。
    /// </summary>
    public sealed class EdgeViewModel
    {
        public EdgeDefinition Edge { get; set; }
    }
}
