using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Demo.WinForms
{
    public sealed class MainForm : Form
    {
        private readonly UiFlowEventSink _eventSink;
        private DataGridView _eventGrid;
        private Label _runtimeStatusValue;
        private Label _runtimeFileValue;
        private Label _runnerStateValue;
        private ListView _tokenList;
        private TextBox _outputSummary;
        private Panel _imagePreview;
        private DefaultDeviceRegistry _devices;
        private NodeRegistry _nodes;
        private IFlowRunner _runner;
        private RuntimeFlowDefinition _runtimeFlow;
        private FakeCameraAdapter _camera;
        private FakeImageSaveAdapter _imageSaver;
        private FakeDatabaseAdapter _database;
        private FlowToken _lastToken;
        private string _runtimePath;
        private string _lastFrameId;
        private string _lastImageSummary;
        private string _lastOutputSummary;
        private int _eventSequence;

        public MainForm()
        {
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
            InitializeRuntimeServices();
            SeedSummaryData();
            AddEvent("System", "Demo initialized", "Fake devices and node factories registered");
        }

        private Control CreateCommandBar()
        {
            var panel = CreateCardPanel();
            panel.Padding = new Padding(8);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.White
            };

            buttons.Controls.Add(CreateCommandButton("Load Runtime Flow", async delegate
            {
                await RunUiActionAsync(LoadRuntimeFlowAsync);
            }));
            buttons.Controls.Add(CreateCommandButton("Start Runner", async delegate
            {
                await RunUiActionAsync(StartRunnerAsync);
            }));
            buttons.Controls.Add(CreateCommandButton("Stop Runner", async delegate
            {
                await RunUiActionAsync(StopRunnerAsync);
            }));
            buttons.Controls.Add(CreateCommandButton("Trigger Manual Start", async delegate
            {
                await RunUiActionAsync(delegate { return TriggerAsync("ManualStart"); });
            }));
            buttons.Controls.Add(CreateCommandButton("Trigger Motion Arrived", async delegate
            {
                await RunUiActionAsync(delegate { return TriggerAsync("ManualStart"); });
            }));

            panel.Controls.Add(buttons);
            return panel;
        }

        private Control CreateInfoPanel()
        {
            var panel = CreateCardPanel();
            panel.Margin = new Padding(0, 10, 10, 0);
            panel.Padding = new Padding(14);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 8,
                BackColor = Color.White
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            layout.Controls.Add(CreateSectionTitle("Runtime"), 0, 0);

            _runtimeStatusValue = CreateValueLabel();
            layout.Controls.Add(CreateInfoBlock("Flow status", _runtimeStatusValue), 0, 1);

            _runtimeFileValue = CreateValueLabel();
            layout.Controls.Add(CreateInfoBlock("Runtime file", _runtimeFileValue), 0, 2);

            _runnerStateValue = CreateValueLabel();
            layout.Controls.Add(CreateInfoBlock("Runner state", _runnerStateValue), 0, 3);

            layout.Controls.Add(CreateSectionTitle("Adapters"), 0, 4);

            var adapterList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(55, 65, 81),
                IntegralHeight = false
            };
            adapterList.Items.Add("Camera: Camera01");
            adapterList.Items.Add("Light: Light01");
            adapterList.Items.Add("Recipe: Recipe01");
            adapterList.Items.Add("ImageSave: ImageSave01");
            adapterList.Items.Add("Database: VisionDb");
            layout.Controls.Add(adapterList, 0, 5);

            layout.Controls.Add(CreateSectionTitle("Entry Signals"), 0, 6);

            var entryList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(55, 65, 81)
            };
            entryList.Items.Add("ManualStart");
            entryList.Items.Add("MotionArrived -> ManualStart");
            layout.Controls.Add(entryList, 0, 7);

            panel.Controls.Add(layout);
            return panel;
        }

        private Control CreateEventPanel(out DataGridView eventGrid)
        {
            var panel = CreateCardPanel();
            panel.Margin = new Padding(0, 10, 10, 0);
            panel.Padding = new Padding(14);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.White
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.Controls.Add(CreateSectionTitle("Runtime Event Log"), 0, 0);

            eventGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(229, 231, 235),
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            eventGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            eventGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 41, 59);
            eventGrid.ColumnHeadersDefaultCellStyle.Font = new Font(Font, FontStyle.Bold);
            eventGrid.DefaultCellStyle.BackColor = Color.White;
            eventGrid.DefaultCellStyle.ForeColor = Color.FromArgb(55, 65, 81);
            eventGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
            eventGrid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 41, 59);
            eventGrid.Columns.Add("Sequence", "#");
            eventGrid.Columns.Add("Time", "Time");
            eventGrid.Columns.Add("Source", "Source");
            eventGrid.Columns.Add("Event", "Event");
            eventGrid.Columns.Add("Detail", "Detail");
            eventGrid.Columns[0].FillWeight = 10;
            eventGrid.Columns[1].FillWeight = 22;
            eventGrid.Columns[2].FillWeight = 24;
            eventGrid.Columns[3].FillWeight = 32;
            eventGrid.Columns[4].FillWeight = 80;

            layout.Controls.Add(eventGrid, 0, 1);
            panel.Controls.Add(layout);
            return panel;
        }

        private Control CreateSummaryPanel(out ListView tokenList, out TextBox outputSummary, out Panel imagePreview)
        {
            var panel = CreateCardPanel();
            panel.Margin = new Padding(0, 10, 0, 0);
            panel.Padding = new Padding(14);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = Color.White
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            layout.Controls.Add(CreateSectionTitle("Active Tokens"), 0, 0);

            tokenList = new ListView
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                View = View.Details,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            tokenList.Columns.Add("Token", 118);
            tokenList.Columns.Add("State", 70);
            tokenList.Columns.Add("Node", 92);
            layout.Controls.Add(tokenList, 0, 1);

            layout.Controls.Add(CreateSectionTitle("Outputs"), 0, 2);

            outputSummary = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(55, 65, 81),
                Text = "Image: -\r\nFrameId: -\r\nRecipeResult: -\r\nDatabaseSave: -",
                ScrollBars = ScrollBars.Vertical
            };
            layout.Controls.Add(outputSummary, 0, 3);

            layout.Controls.Add(CreateSectionTitle("Image Preview"), 0, 4);

            imagePreview = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 41, 55)
            };
            imagePreview.Paint += PaintImagePreviewPlaceholder;
            layout.Controls.Add(imagePreview, 0, 5);

            panel.Controls.Add(layout);
            return panel;
        }

        private static Panel CreateCardPanel()
        {
            return new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private static Button CreateCommandButton(string text, EventHandler handler)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 34,
                Margin = new Padding(4, 6, 8, 6),
                Padding = new Padding(12, 0, 12, 0),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += handler;
            return button;
        }

        private static Label CreateSectionTitle(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Label CreateValueLabel()
        {
            return new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(30, 41, 59),
                TextAlign = ContentAlignment.TopLeft
            };
        }

        private static Control CreateInfoBlock(string caption, Label value)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(8)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(100, 116, 139),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            panel.Controls.Add(value, 0, 1);
            return panel;
        }

        private void SeedSummaryData()
        {
            _tokenList.Items.Clear();
            var idle = new ListViewItem(new[] { "token-000", "Idle", "entry" });
            _tokenList.Items.Add(idle);
            _outputSummary.Text = "Image: waiting\r\nFrameId: -\r\nRecipeResult: -\r\nDatabaseSave: -";
        }

        private void AddEvent(string source, string eventName, string detail)
        {
            _eventSequence++;
            _eventGrid.Rows.Insert(0, _eventSequence.ToString(), DateTime.Now.ToString("HH:mm:ss.fff"), source, eventName, detail);
            if (_eventGrid.Rows.Count > 200)
            {
                _eventGrid.Rows.RemoveAt(_eventGrid.Rows.Count - 1);
            }
        }

        private void RefreshTokenSummary(string entryName)
        {
            _tokenList.Items.Clear();
            var tokenId = _lastToken == null ? "token-" + _eventSequence.ToString("000") : _lastToken.TokenId;
            _tokenList.Items.Add(new ListViewItem(new[] { tokenId, _runner != null && _runner.IsRunning ? "Completed" : "Stopped", entryName }));
            _outputSummary.Text = _lastOutputSummary ?? "Image: waiting\r\nFrameId: -\r\nRecipeResult: -\r\nDatabaseSave: -";
            _imagePreview.Invalidate();
        }

        private void InitializeRuntimeServices()
        {
            _camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 20
            };
            var recipe = new FakeRecipeAdapter("Recipe01");
            recipe.DefaultOutputs["IsOk"] = true;
            recipe.DefaultOutputs["ResultImage"] = new FakeVisionImage("recipe-result", 640, 480, "Mono8", null);
            _imageSaver = new FakeImageSaveAdapter("ImageSave01");
            _database = new FakeDatabaseAdapter("VisionDb");

            _devices = new DefaultDeviceRegistry();
            _devices.RegisterCamera(_camera);
            _devices.RegisterLight(new FakeLightAdapter("Light01"));
            _devices.RegisterRecipe(recipe);
            _devices.RegisterImageSaver(_imageSaver);
            _devices.RegisterDatabase(_database);

            _nodes = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(_nodes);
        }

        private Task RunUiActionAsync(Func<Task> action)
        {
            return RunUiActionCoreAsync(action);
        }

        private async Task RunUiActionCoreAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AddEvent("Error", ex.GetType().Name, ex.Message);
                _runnerStateValue.Text = _runner != null && _runner.IsRunning ? "Running" : "Stopped";
            }
        }

        private async Task LoadRuntimeFlowAsync()
        {
            _runtimePath = ResolveRuntimePath();
            _runtimeFlow = RuntimeFlowSerializer.Load(_runtimePath);
            _runner = new FlowEngine(_nodes, _eventSink, _devices).CreateRunner(_runtimeFlow);
            _runtimeStatusValue.Text = _runtimeFlow.FlowName + " (" + _runtimeFlow.Nodes.Count + " nodes)";
            _runtimeFileValue.Text = _runtimePath;
            _runnerStateValue.Text = "Loaded";
            AddEvent("Runtime", "Loaded", _runtimeFlow.FlowId);
            await Task.FromResult(0);
        }

        private async Task StartRunnerAsync()
        {
            await EnsureRunnerLoadedAsync().ConfigureAwait(true);
            await _runner.StartAsync(CancellationToken.None).ConfigureAwait(true);
            _runnerStateValue.Text = "Running";
        }

        private async Task StopRunnerAsync()
        {
            if (_runner == null)
            {
                _runnerStateValue.Text = "Stopped";
                return;
            }

            await _runner.StopAsync(CancellationToken.None).ConfigureAwait(true);
            _runnerStateValue.Text = "Stopped";
        }

        private async Task TriggerAsync(string entryName)
        {
            await EnsureRunnerLoadedAsync().ConfigureAwait(true);
            if (!_runner.IsRunning)
            {
                await _runner.StartAsync(CancellationToken.None).ConfigureAwait(true);
                _runnerStateValue.Text = "Running";
            }

            _lastToken = new FlowToken
            {
                TokenId = Guid.NewGuid().ToString("N"),
                ProductId = "DemoProduct",
                WorkpieceId = DateTime.Now.ToString("yyyyMMddHHmmss"),
                PositionId = "Manual"
            };

            await _runner.TriggerAsync(entryName, _lastToken, CancellationToken.None).ConfigureAwait(true);
            RefreshTokenSummary(entryName);
        }

        private async Task EnsureRunnerLoadedAsync()
        {
            if (_runner == null)
            {
                await LoadRuntimeFlowAsync().ConfigureAwait(true);
            }
        }

        private string ResolveRuntimePath()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            for (var depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(root); depth++)
            {
                var candidate = Path.Combine(root, "samples", "flows", "single-shot.flowruntime");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(root);
                root = parent == null ? null : parent.FullName;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\samples\flows\single-shot.flowruntime"));
        }

        private void HandleRuntimeEvent(FlowRuntimeEvent runtimeEvent)
        {
            if (runtimeEvent == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<FlowRuntimeEvent>(HandleRuntimeEvent), runtimeEvent);
                return;
            }

            var source = string.IsNullOrWhiteSpace(runtimeEvent.NodeId) ? runtimeEvent.FlowId : runtimeEvent.NodeId;
            var detail = string.IsNullOrWhiteSpace(runtimeEvent.Message) ? runtimeEvent.OutputPort : runtimeEvent.Message;
            AddEvent(source, runtimeEvent.EventType.ToString(), detail);
            UpdateOutputSummary(runtimeEvent);
        }

        private void UpdateOutputSummary(FlowRuntimeEvent runtimeEvent)
        {
            if (runtimeEvent.EventType != FlowRuntimeEventType.OutputProduced ||
                runtimeEvent.Data == null ||
                !runtimeEvent.Data.ContainsKey("VariableName"))
            {
                return;
            }

            var variableName = Convert.ToString(runtimeEvent.Data["VariableName"]);
            object value = runtimeEvent.Data.ContainsKey("Value") ? runtimeEvent.Data["Value"] : null;

            var image = value as IVisionImage;
            if (image != null)
            {
                _lastImageSummary = BuildImageSummary(image);
            }

            if (string.Equals(variableName, "cam_callback_1.FrameId", StringComparison.OrdinalIgnoreCase))
            {
                _lastFrameId = Convert.ToString(value);
            }

            if (string.Equals(variableName, "image_save_1.ImagePath", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variableName, "image_save_1.ResultImagePath", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variableName, "recipe_1.IsOk", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variableName, "db_save_1.Saved", StringComparison.OrdinalIgnoreCase))
            {
                BuildOutputSummary();
            }
            else if (image != null || !string.IsNullOrWhiteSpace(_lastFrameId))
            {
                BuildOutputSummary();
            }

            if (image != null)
            {
                _imagePreview.Invalidate();
            }
        }

        private void BuildOutputSummary()
        {
            var imagePath = FindLatestOutput("image_save_1.ImagePath");
            var resultImagePath = FindLatestOutput("image_save_1.ResultImagePath");
            var isOk = FindLatestOutput("recipe_1.IsOk");
            var saved = FindLatestOutput("db_save_1.Saved");
            _lastOutputSummary =
                "Image: " + (_lastImageSummary ?? "-") + "\r\n" +
                "FrameId: " + (_lastFrameId ?? "-") + "\r\n" +
                "RecipeResult: " + (isOk == null ? "-" : "IsOk=" + isOk) + "\r\n" +
                "ImagePath: " + (imagePath ?? "-") + "\r\n" +
                "ResultImagePath: " + (resultImagePath ?? "-") + "\r\n" +
                "DatabaseSave: " + (saved ?? "-");
            _outputSummary.Text = _lastOutputSummary;
        }

        private static string BuildImageSummary(IVisionImage image)
        {
            if (image == null)
            {
                return null;
            }

            byte[] bytes;
            var byteText = image.TryGetBytes(out bytes) && bytes != null
                ? bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes"
                : "bytes unavailable";
            var storageText = image.NativeImage == null ? "managed" : "native";
            var disposedText = image.IsDisposed ? ", disposed" : string.Empty;
            return image.Width.ToString(CultureInfo.InvariantCulture) +
                "x" +
                image.Height.ToString(CultureInfo.InvariantCulture) +
                " " +
                image.PixelFormat +
                ", " +
                byteText +
                ", " +
                storageText +
                disposedText;
        }

        private object FindLatestOutput(string variableName)
        {
            for (var row = 0; row < _eventGrid.Rows.Count; row++)
            {
                // The event grid is for humans; runtime output values are easier to capture directly.
            }

            return _eventSink.TryGetOutput(variableName);
        }

        private void PaintImagePreviewPlaceholder(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(31, 41, 55));
            using (var pen = new Pen(Color.FromArgb(71, 85, 105), 1))
            {
                var bounds = _imagePreview.ClientRectangle;
                bounds.Inflate(-18, -18);
                e.Graphics.DrawRectangle(pen, bounds);
                e.Graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                e.Graphics.DrawLine(pen, bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
            }

            using (var brush = new SolidBrush(Color.FromArgb(203, 213, 225)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                e.Graphics.DrawString(_lastImageSummary ?? "waiting for image output", Font, brush, _imagePreview.ClientRectangle, format);
            }
        }

        private sealed class UiFlowEventSink : IFlowEventSink
        {
            private readonly object _gate = new object();
            private readonly Action<FlowRuntimeEvent> _onEvent;
            private readonly Dictionary<string, object> _outputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public UiFlowEventSink(Action<FlowRuntimeEvent> onEvent)
            {
                _onEvent = onEvent;
            }

            public Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (runtimeEvent == null)
                {
                    throw new ArgumentNullException("runtimeEvent");
                }

                if (runtimeEvent.EventType == FlowRuntimeEventType.OutputProduced &&
                    runtimeEvent.Data != null &&
                    runtimeEvent.Data.ContainsKey("VariableName"))
                {
                    lock (_gate)
                    {
                        _outputs[Convert.ToString(runtimeEvent.Data["VariableName"])] =
                            runtimeEvent.Data.ContainsKey("Value") ? runtimeEvent.Data["Value"] : null;
                    }
                }

                _onEvent(runtimeEvent);
                return Task.FromResult(0);
            }

            public object TryGetOutput(string variableName)
            {
                lock (_gate)
                {
                    object value;
                    return _outputs.TryGetValue(variableName, out value) ? value : null;
                }
            }
        }
    }
}
