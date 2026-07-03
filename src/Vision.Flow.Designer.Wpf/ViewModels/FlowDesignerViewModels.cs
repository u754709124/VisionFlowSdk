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
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Designer.Wpf.ViewModels
{
    // 璁捐鍣?ViewModel 鏄緵璁捐鍣ㄦ帶浠跺叡浜殑杞婚噺鐘舵€佽浇浣撱€?
    /// <summary>
    /// 璁捐鍣ㄦ牴瑙嗗浘妯″瀷锛屾壙杞藉綋鍓嶈璁℃€佹祦绋嬫枃妗ｃ€?
    /// </summary>
    public sealed class FlowDesignerViewModel
    {
        public FlowDesignDocument Document { get; set; }
    }
}
