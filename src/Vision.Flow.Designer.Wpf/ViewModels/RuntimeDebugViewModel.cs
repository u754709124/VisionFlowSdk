using System.Collections.Generic;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
    /// <summary>
    /// 运行调试视图模型，保存设计器调试期间接收到的运行事件�?
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
