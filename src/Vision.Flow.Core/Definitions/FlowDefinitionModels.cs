using System.Collections.Generic;

namespace Vision.Flow.Core
{
    /// <summary>
    /// 设计态流程文档，包含可发布的运行态定义和设计器视图状态。
    /// </summary>
    public sealed class FlowDesignDocument
    {
        public FlowDesignDocument()
        {
            SchemaVersion = 1;
            Runtime = new RuntimeFlowDefinition();
            View = new FlowViewState();
        }

        /// <summary>
        /// 流程唯一标识，用于设计文件、运行文件和运行事件之间建立关联。
        /// </summary>
        public string FlowId { get; set; }

        /// <summary>
        /// 面向人的流程名称，不参与执行调度。
        /// </summary>
        public string FlowName { get; set; }

        /// <summary>
        /// 文件结构版本，用于后续兼容升级。
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// 可发布到生产环境的运行态定义。
        /// </summary>
        public RuntimeFlowDefinition Runtime { get; set; }

        /// <summary>
        /// 仅供设计器使用的画布状态，发布 `.flowruntime` 时必须移除。
        /// </summary>
        public FlowViewState View { get; set; }
    }

    /// <summary>
    /// 设计器画布视图状态，不允许进入生产运行态文件。
    /// </summary>
    public sealed class FlowViewState
    {
        public const double DefaultCanvasWidth = 1800;
        public const double DefaultCanvasHeight = 1100;

        public FlowViewState()
        {
            Zoom = 1.0;
            CanvasWidth = DefaultCanvasWidth;
            CanvasHeight = DefaultCanvasHeight;
            Nodes = new Dictionary<string, NodeViewState>();
        }

        public double Zoom { get; set; }

        public double OffsetX { get; set; }

        public double OffsetY { get; set; }

        /// <summary>
        /// 设计器画布宽度，仅用于 `.flowdesign` 视图状态，发布运行态时必须移除。
        /// </summary>
        public double CanvasWidth { get; set; }

        /// <summary>
        /// 设计器画布高度，仅用于 `.flowdesign` 视图状态，发布运行态时必须移除。
        /// </summary>
        public double CanvasHeight { get; set; }

        public Dictionary<string, NodeViewState> Nodes { get; set; }
    }

    /// <summary>
    /// 单个节点在设计器画布上的显示状态。
    /// </summary>
    public sealed class NodeViewState
    {
        public double X { get; set; }

        public double Y { get; set; }

        public bool IsCollapsed { get; set; }
    }

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

    /// <summary>
    /// 控制流连线定义，连接上游输出端口和下游输入端口。
    /// </summary>
    public sealed class EdgeDefinition
    {
        public string FromNodeId { get; set; }

        public string FromPort { get; set; }

        public string ToNodeId { get; set; }

        public string ToPort { get; set; }
    }

    /// <summary>
    /// 流程入口定义，用于生产上位机从外部事件触发指定节点。
    /// </summary>
    public sealed class FlowEntryDefinition
    {
        public string EntryName { get; set; }

        public string TargetNodeId { get; set; }
    }
}
