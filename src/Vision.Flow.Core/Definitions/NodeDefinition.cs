using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Definitions
{
    /// <summary>
    /// 运行态节点定义，保存节点类型、配置和变量绑定。
    /// </summary>
    public sealed class NodeDefinition
    {
        public NodeDefinition()
        {
            Settings = new Dictionary<string, object>();
            InputBindings = new Dictionary<string, VariableBinding>();
        }

        /// <summary>
        /// 节点实例 ID，是变量名和连线引用的稳定键。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 节点类型，必须使用 `FlowNodeTypes` 中的已注册协议值。
        /// </summary>
        public string Type { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        /// <summary>
        /// 节点配置字典，键应优先使用 `FlowSettingNames` 常量。
        /// </summary>
        public Dictionary<string, object> Settings { get; set; }

        /// <summary>
        /// 输入端口到变量表达式的绑定，数据主要通过该结构跨节点传递。
        /// </summary>
        public Dictionary<string, VariableBinding> InputBindings { get; set; }
    }
}
