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
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.Theming;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 设计器核心状态、构造逻辑和公开集成点保留在根文件。
    internal enum DesignerInteractionMode
    {
        Edit = 0,
        DebugRun = 1
    }

    internal enum DebugDrawerPreference
    {
        Auto = 0,
        Open = 1,
        Closed = 2
    }

    /// <summary>
    /// 指定设计器命令栏由控件内部承载，还是交由业务宿主承载。
    /// </summary>
    public enum FlowDesignerToolbarPlacement
    {
        Internal = 0,
        External = 1
    }

    /// <summary>
    /// 用户处理未应用节点属性时的选择。
    /// </summary>
    public enum PendingPropertyChangesDecision
    {
        Apply = 0,
        Discard = 1,
        Cancel = 2
    }

    public sealed class FlowDesignerOptions
    {
        public FlowDesignerOptions()
        {
            LoadSampleOnStartup = true;
            ShowStandaloneDocumentCommands = true;
            ToolbarPlacement = FlowDesignerToolbarPlacement.Internal;
        }

        public bool LoadSampleOnStartup { get; set; }

        /// <summary>
        /// 是否显示设计器自带的新建、示例、打开、保存和发布命令。
        /// 嵌入业务宿主并由宿主管理复合配置文件时可关闭这些命令。
        /// </summary>
        public bool ShowStandaloneDocumentCommands { get; set; }

        /// <summary>
        /// 命令栏承载位置。外置时可通过 FlowDesignerControl.ToolbarView 挂入宿主。
        /// </summary>
        public FlowDesignerToolbarPlacement ToolbarPlacement { get; set; }

        public IDeviceRegistry DebugDevices { get; set; }

        /// <summary>
        /// 由嵌入式宿主提供显示文本与稳定协议值分离的固定值候选项。
        /// </summary>
        public Func<NodeSettingDescriptor, IEnumerable<NodeSettingConstantOption>> SettingConstantOptionsProvider { get; set; }

        /// <summary>
        /// 自定义未应用属性决策。测试或业务宿主可提供确定性决策；为空时使用设计器对话框。
        /// </summary>
        public Func<PendingPropertyChangesDecision> PendingPropertyChangesPrompt { get; set; }
    }

    public sealed partial class FlowDesignerControl : UserControl
    {
        private const string DefaultEntryName = "ManualStart";
        private const double GridSize = 32;
        private const double CanvasExpansionMargin = 160;
        private const double CanvasExpansionStep = 512;
        private const double NodeBoundsFallbackWidth = 220;
        private const double NodeBoundsFallbackHeight = 182;
        private const string PaletteNodeTypeDragFormat = "Vision.Flow.Designer.Wpf.NodePalette.NodeType";

        private readonly NodeRegistry _nodeRegistry;
        private readonly Dictionary<string, NodeCardControl> _nodeCards;
        private readonly Dictionary<string, DateTime> _nodeStartTimes;
        private readonly NodePaletteControl _palette;
        private readonly PropertyPanelControl _properties;
        private readonly EntryTriggerPanelControl _entryTriggerPanel;
        private readonly RuntimeDebugPanelControl _debug;
        private readonly EdgeLayerControl _edges;
        private readonly FlowMiniMapControl _miniMap;
        private readonly FlowDesignerOptions _options;
        private readonly Canvas _nodeLayer;
        private readonly TextBlock _statusText;
        private readonly FrameworkElement _toolbarView;
        private Button _editModeButton;
        private Button _debugModeButton;
        private Button _newButton;
        private Button _sampleButton;
        private Button _openButton;
        private Button _saveButton;
        private Button _publishButton;
        private Button _debugRunButton;
        private Button _stopButton;
        private TextBlock _zoomText;
        private Rectangle _gridLayer;
        private RowDefinition _debugRowDefinition;
        private DebugDrawerPreference _debugDrawerPreference;

        private DesignerInteractionMode _interactionMode;
        private FlowDesignDocument _document;
        private NodeDefinition _selectedNode;
        private NodeDefinition _propertyDraftNode;
        private NodeDefinition _propertyBaselineNode;
        private NodeDescriptor _propertyDraftDescriptor;
        private string _propertyDraftDescriptorState;
        private bool _isReconcilingPropertyDescriptor;
        private EdgeDefinition _selectedEdge;
        private IFlowRunner _runner;
        private Grid _surface;
        private ScrollViewer _canvasScroll;
        private ScaleTransform _canvasScale;
        private double _canvasWidth;
        private double _canvasHeight;
        private Point _dragOffset;
        private NodeCardControl _dragCard;
        private bool _isPanning;
        private Point _panStart;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;
        private bool _isCanvasFrameScheduled;
        private bool _hasPendingCanvasPan;
        private double _pendingPanHorizontalOffset;
        private double _pendingPanVerticalOffset;
        private bool _hasPendingCanvasZoom;
        private double _pendingCanvasZoom;
        private Point _pendingZoomAnchor;
        private bool _isRenderingEdges;
        private bool _hasDeferredEdgeRefresh;
        private bool _isConnecting;
        private bool _isDebugRunning;
        private NodeDefinition _connectionSourceNode;
        private string _connectionSourcePort;
        private string _selectedDebugEntryName;
        private Point _connectionStartPoint;

        public FlowDesignerControl()
            : this(null, null, null)
        {
        }

        public FlowDesignerControl(NodeRegistry nodeRegistry)
            : this(nodeRegistry, null, null)
        {
        }

        public FlowDesignerControl(NodeRegistry nodeRegistry, IDeviceRegistry debugDevices)
            : this(nodeRegistry, debugDevices, null)
        {
        }

        public FlowDesignerControl(NodeRegistry nodeRegistry, IDeviceRegistry debugDevices, FlowDesignerOptions options)
        {
            _options = options ?? new FlowDesignerOptions();
            _nodeRegistry = nodeRegistry ?? CreateDefaultNodeRegistry();
            _nodeCards = new Dictionary<string, NodeCardControl>(StringComparer.OrdinalIgnoreCase);
            _nodeStartTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            _interactionMode = DesignerInteractionMode.Edit;
            _palette = new NodePaletteControl();
            _properties = new PropertyPanelControl(_options.SettingConstantOptionsProvider);
            _properties.ApplyRequested += delegate
            {
                string error;
                TryApplyPendingPropertyChanges(out error);
            };
            _properties.ResetRequested += DiscardPendingPropertyChanges;
            _entryTriggerPanel = new EntryTriggerPanelControl
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _entryTriggerPanel.EntrySelected += delegate(FlowEntryDefinition entry)
            {
                _selectedDebugEntryName = entry == null ? null : entry.EntryName;
                UpdateInteractionModeUi();
            };
            _debug = new RuntimeDebugPanelControl();
            _debug.ExpansionChanged += OnDebugDrawerExpansionChanged;
            _edges = new EdgeLayerControl();
            _edges.EdgeSelected += SelectEdge;
            _edges.EdgeDeleteRequested += DeleteEdge;
            _miniMap = new FlowMiniMapControl();
            _miniMap.ViewportRequested += NavigateToMiniMapViewport;
            _canvasWidth = FlowViewState.DefaultCanvasWidth;
            _canvasHeight = FlowViewState.DefaultCanvasHeight;
            _nodeLayer = new Canvas
            {
                Width = _canvasWidth,
                Height = _canvasHeight,
                Background = null,
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };
            TextOptions.SetTextFormattingMode(_nodeLayer, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(_nodeLayer, TextRenderingMode.ClearType);
            _nodeLayer.LayoutUpdated += OnNodeLayerLayoutUpdated;
            _statusText = new TextBlock
            {
                Foreground = BrushFromRgb(100, 116, 139),
                VerticalAlignment = VerticalAlignment.Center
            };

            DebugDevices = debugDevices ?? _options.DebugDevices;
            InitializeResources();
            _toolbarView = CreateToolbar();
            Content = CreateShell();
            _palette.SetDescriptors(_nodeRegistry.Descriptors.OrderBy(x => x.Category).ThenBy(x => x.DisplayName));
            _palette.NodeRequested += AddNodeFromPalette;
            _palette.NodeDragRequested += OnPaletteNodeDragRequested;
            _debug.NodeRequested += SelectNodeById;
            PreviewKeyDown += OnPreviewKeyDown;
            Unloaded += delegate { CancelCanvasInteractionFrame(); };
            Focusable = true;
            if (_options.LoadSampleOnStartup)
            {
                LoadCoreBasicTemplate();
            }
            else
            {
                CreateNewDesign();
            }

            UpdateInteractionModeUi();
        }

        public IDeviceRegistry DebugDevices { get; set; }

        public NodeRegistry NodeRegistry
        {
            get { return _nodeRegistry; }
        }

        public FlowDesignerOptions Options
        {
            get { return _options; }
        }

        /// <summary>
        /// 设计器命令栏视图。ToolbarPlacement 为 External 时，业务宿主可将该单例元素放入自己的命令区。
        /// </summary>
        public FrameworkElement ToolbarView
        {
            get { return _toolbarView; }
        }

        /// <summary>
        /// 在宿主更新动态固定值候选项后，重新呈现当前节点属性面板。
        /// </summary>
        public void RefreshSelectedNodeProperties()
        {
            RenderProperties();
        }

        /// <summary>
        /// 由嵌入式宿主原位替换环境变量定义并刷新当前属性草稿。
        /// </summary>
        public void UpdateEnvironmentVariables(
            IEnumerable<EnvironmentVariableDefinition> definitions)
        {
            if (_document == null || _document.Runtime == null)
                return;

            _document.Runtime.EnvironmentVariables =
                (definitions ?? Enumerable.Empty<EnvironmentVariableDefinition>())
                    .Select(x => x == null
                        ? null
                        : new EnvironmentVariableDefinition
                        {
                            Id = x.Id,
                            Name = x.Name,
                            DataType = x.DataType,
                            DefaultValue = x.DefaultValue
                        })
                    .ToList();
            RenderProperties();
        }

        /// <summary>
        /// 由嵌入式宿主原位替换全局变量定义并刷新变量候选和动态节点描述符。
        /// </summary>
        public void UpdateGlobalVariables(
            IEnumerable<GlobalVariableDefinition> definitions)
        {
            if (_document == null || _document.Runtime == null)
                return;

            _document.Runtime.GlobalVariables =
                (definitions ?? Enumerable.Empty<GlobalVariableDefinition>())
                    .Select(x => x == null
                        ? null
                        : new GlobalVariableDefinition
                        {
                            Id = x.Id,
                            Name = x.Name,
                            DataType = x.DataType,
                            DefaultValue = x.DefaultValue
                        })
                    .ToList();
            RenderProperties();
        }

        private bool CanEditDocument
        {
            get { return _interactionMode == DesignerInteractionMode.Edit; }
        }

        private bool IsDebugRunMode
        {
            get { return _interactionMode == DesignerInteractionMode.DebugRun; }
        }
    }
}
