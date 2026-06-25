using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Demo.WinForms
{
    // Layout helpers build the production demo command bar, cards, grids, and image preview.
    public sealed partial class MainForm
    {
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
            buttons.Controls.Add(CreateCommandButton("Browse Runtime...", async delegate
            {
                await RunUiActionAsync(BrowseRuntimeFlowAsync);
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
            buttons.Controls.Add(CreateCommandButton("Trigger Selected Entry", async delegate
            {
                await RunUiActionAsync(delegate { return TriggerAsync(GetSelectedEntryName()); });
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

            _entrySelector = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(55, 65, 81),
                FlatStyle = FlatStyle.Flat
            };
            layout.Controls.Add(_entrySelector, 0, 7);

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
    }
}
