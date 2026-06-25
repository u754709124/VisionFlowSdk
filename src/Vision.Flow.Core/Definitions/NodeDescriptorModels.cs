using System.Collections.Generic;

namespace Vision.Flow.Core
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

    /// <summary>
    /// 节点端口描述，约束端口名、方向和控制流/数据类型。
    /// </summary>
    public sealed class NodePortDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Direction { get; set; }

        public string DataType { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }

    /// <summary>
    /// 节点配置项描述，驱动设计器属性编辑和运行前校验。
    /// </summary>
    public sealed class NodeSettingDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string DataType { get; set; }

        public object DefaultValue { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }

    /// <summary>
    /// 节点输出变量描述，供变量选择器和绑定校验使用。
    /// </summary>
    public sealed class NodeOutputDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string DataType { get; set; }

        public string Description { get; set; }
    }
}
