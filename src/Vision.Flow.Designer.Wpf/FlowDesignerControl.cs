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
using Vision.Flow.Core;
using Vision.Flow.Nodes;
using ShapesPath = System.Windows.Shapes.Path;

namespace Vision.Flow.Designer.Wpf
{
    // 设计器核心状态、构造逻辑和公开集成点保留在根文件。
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
        private const double CanvasWidth = 1800;
        private const double CanvasHeight = 1100;
        private const double GridSize = 32;

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
        private TextBlock _zoomText;

        private FlowDesignDocument _document;
        private NodeDefinition _selectedNode;
        private EdgeDefinition _selectedEdge;
        private IFlowRunner _runner;
        private Grid _surface;
        private ScrollViewer _canvasScroll;
        private ScaleTransform _canvasScale;
        private Point _dragOffset;
        private NodeCardControl _dragCard;
        private bool _isPanning;
        private Point _panStart;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;
        private bool _isRenderingEdges;
        private bool _hasDeferredEdgeRefresh;
        private bool _isConnecting;
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
            _palette = new NodePaletteControl();
            _properties = new PropertyPanelControl();
            _debug = new RuntimeDebugPanelControl();
            _edges = new EdgeLayerControl();
            _edges.EdgeSelected += SelectEdge;
            _edges.EdgeDeleteRequested += DeleteEdge;
            _nodeLayer = new Canvas
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                Background = null
            };
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
            _debug.NodeRequested += SelectNodeById;
            PreviewKeyDown += OnPreviewKeyDown;
            Focusable = true;
            if (_options.LoadSampleOnStartup)
            {
                LoadSingleShotTemplate();
            }
            else
            {
                CreateNewDesign();
            }
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
    }
}
