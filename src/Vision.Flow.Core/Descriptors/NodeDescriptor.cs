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

namespace Vision.Flow.Core.Descriptors
{
    /// <summary>
    /// 节点描述符，向设计器和校验器暴露节点的端口、设置和输出变量。
    /// </summary>
    public sealed class NodeDescriptor
    {
        public NodeDescriptor()
        {
            InputPorts = new List<NodePortDescriptor>();
            OutputPorts = new List<NodePortDescriptor>();
            Settings = new List<NodeSettingDescriptor>();
            Outputs = new List<NodeOutputDescriptor>();
        }

        /// <summary>
        /// 节点类型协议值，应与节点工厂注册的 `NodeType` 完全一致。
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>
        /// 设计器节点库显示名称。
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 设计器节点库分类。
        /// </summary>
        public string Category { get; set; }

        public string Version { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// 输入端口列表，用于属性面板变量绑定和连线校验。
        /// </summary>
        public List<NodePortDescriptor> InputPorts { get; set; }

        /// <summary>
        /// 输出端口列表，用于控制流调度和设计器连线。
        /// </summary>
        public List<NodePortDescriptor> OutputPorts { get; set; }

        /// <summary>
        /// 节点配置项列表，用于动态属性面板和发布前校验。
        /// </summary>
        public List<NodeSettingDescriptor> Settings { get; set; }

        /// <summary>
        /// 节点运行后写入变量池的输出变量定义。
        /// </summary>
        public List<NodeOutputDescriptor> Outputs { get; set; }
    }
}
