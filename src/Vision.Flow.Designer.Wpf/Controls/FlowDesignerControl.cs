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
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 设计器核心状态、构造逻辑和公开集成点保留在根文件。
    internal enum DesignerInteractionMode
    {
        Edit = 0,
        DebugRun = 1
    }

    public sealed class FlowDesignerOptions
    {
        public FlowDesignerOptions()
        {
            LoadSampleOnStartup = true;
        }

        public bool LoadSampleOnStartup { get; set; }

        public IDeviceRegistry DebugDevices { get; set; }
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
        private readonly RuntimeDebugPanelControl _debug;
        private readonly EdgeLayerControl _edges;
        private readonly FlowDesignerOptions _options;
        private readonly Canvas _nodeLayer;
        private readonly TextBlock _statusText;
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

        private DesignerInteractionMode _interactionMode;
        private FlowDesignDocument _document;
        private NodeDefinition _selectedNode;
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
        private bool _isRenderingEdges;
        private bool _hasDeferredEdgeRefresh;
        private bool _isConnecting;
        private bool _isDebugRunning;
        private NodeDefinition _connectionSourceNode;
        private string _connectionSourcePort;
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
            _properties = new PropertyPanelControl();
            _debug = new RuntimeDebugPanelControl();
            _edges = new EdgeLayerControl();
            _edges.EdgeSelected += SelectEdge;
            _edges.EdgeDeleteRequested += DeleteEdge;
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
            TextOptions.SetTextFormattingMode(_nodeLayer, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(_nodeLayer, TextRenderingMode.ClearType);
            _nodeLayer.LayoutUpdated += OnNodeLayerLayoutUpdated;
            _statusText = new TextBlock
            {
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };

            DebugDevices = debugDevices ?? _options.DebugDevices;
            InitializeResources();
            Content = CreateShell();
            _palette.SetDescriptors(_nodeRegistry.Descriptors.OrderBy(x => x.Category).ThenBy(x => x.DisplayName));
            _palette.NodeRequested += AddNodeFromPalette;
            _palette.NodeDragRequested += OnPaletteNodeDragRequested;
            _debug.NodeRequested += SelectNodeById;
            PreviewKeyDown += OnPreviewKeyDown;
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
