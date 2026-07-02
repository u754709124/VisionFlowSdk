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
    /// 生产运行态流程定义，WinForms 上位机加载 `.flowruntime` 后由 `FlowRunner` 执行。
    /// </summary>
    public sealed class RuntimeFlowDefinition
    {
        public RuntimeFlowDefinition()
        {
            SchemaVersion = 1;
            Nodes = new List<NodeDefinition>();
            Edges = new List<EdgeDefinition>();
            Entries = new List<FlowEntryDefinition>();
            Settings = new Dictionary<string, object>();
        }

        public string FlowId { get; set; }

        public string FlowName { get; set; }

        public int SchemaVersion { get; set; }

        public string Version { get; set; }

        /// <summary>
        /// 运行态节点集合，只包含执行所需信息。
        /// </summary>
        public List<NodeDefinition> Nodes { get; set; }

        /// <summary>
        /// 控制流连线集合，按输出端口驱动后续节点调度。
        /// </summary>
        public List<EdgeDefinition> Edges { get; set; }

        /// <summary>
        /// 外部事件可触发的流程入口。
        /// </summary>
        public List<FlowEntryDefinition> Entries { get; set; }

        public Dictionary<string, object> Settings { get; set; }
    }
}
