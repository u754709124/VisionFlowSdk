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
using Vision.Flow.Nodes;
using ShapesPath = System.Windows.Shapes.Path;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
    // 设计器 ViewModel 是供设计器控件共享的轻量状态载体。
    /// <summary>
    /// 设计器根视图模型，承载当前设计态流程文档。
    /// </summary>
    public sealed class FlowDesignerViewModel
    {
        public FlowDesignDocument Document { get; set; }
    }
}
