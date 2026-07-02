using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vision.Flow.Nodes;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Demo.WinForms
{
    // 主窗体状态和构造留在根文件，具体工作流细节放在 partial 文件中。
    public sealed partial class MainForm : Form
    {
        private readonly UiFlowEventSink _eventSink;
        private DataGridView _eventGrid;
        private Label _runtimeStatusValue;
        private Label _runtimeFileValue;
        private Label _runnerStateValue;
        private ComboBox _entrySelector;
        private ListView _tokenList;
        private TextBox _outputSummary;
        private Panel _imagePreview;
        private NodeRegistry _nodes;
        private IFlowRunner _runner;
        private RuntimeFlowDefinition _runtimeFlow;
        private FlowToken _lastToken;
        private string _runtimePath;
        private string _requestedRuntimePath;
        private string _lastOutputSummary;
        private int _eventSequence;

        public MainForm()
            : this(null)
        {
        }

        public MainForm(string runtimePath)
        {
            _requestedRuntimePath = runtimePath;
            _eventSink = new UiFlowEventSink(HandleRuntimeEvent);
            Text = "Vision Flow Runtime Demo";
            Width = 1280;
            Height = 780;
            MinimumSize = new Size(1100, 680);
            BackColor = Color.FromArgb(246, 248, 251);
            Font = new Font("Segoe UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(12),
                BackColor = BackColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(CreateCommandBar(), 0, 0);

            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = BackColor
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));

            var leftPanel = CreateInfoPanel();
            var centerPanel = CreateEventPanel(out _eventGrid);
            var rightPanel = CreateSummaryPanel(out _tokenList, out _outputSummary, out _imagePreview);

            content.Controls.Add(leftPanel, 0, 0);
            content.Controls.Add(centerPanel, 1, 0);
            content.Controls.Add(rightPanel, 2, 0);
            root.Controls.Add(content, 0, 1);

            Controls.Add(root);

            _runtimeStatusValue.Text = "No runtime loaded";
            _runtimeFileValue.Text = "-";
            _runnerStateValue.Text = "Stopped";
            ResetEntrySelector();
            InitializeRuntimeServices();
            SeedSummaryData();
            AddEvent("System", "Demo initialized", "Core node factories registered");
        }
    }
}
