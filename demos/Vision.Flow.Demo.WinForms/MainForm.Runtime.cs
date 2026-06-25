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
    // 运行辅助方法负责加载 flowruntime 文件、管理 FlowRunner 生命周期并触发入口。
    public sealed partial class MainForm
    {
        private void SeedSummaryData()
        {
            _tokenList.Items.Clear();
            var idle = new ListViewItem(new[] { "token-000", "Idle", "entry" });
            _tokenList.Items.Add(idle);
            _outputSummary.Text = "Image: waiting\r\nFrameId: -\r\nRecipeResult: -\r\nDatabaseSave: -";
        }

        private void ResetEntrySelector()
        {
            if (_entrySelector == null)
            {
                return;
            }

            _entrySelector.Items.Clear();
            _entrySelector.Items.Add("ManualStart");
            _entrySelector.SelectedIndex = 0;
        }

        private void PopulateEntrySelector(RuntimeFlowDefinition runtime)
        {
            ResetEntrySelector();
            if (runtime == null || runtime.Entries == null || runtime.Entries.Count == 0)
            {
                return;
            }

            _entrySelector.Items.Clear();
            foreach (var entry in runtime.Entries)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.EntryName))
                {
                    _entrySelector.Items.Add(entry.EntryName);
                }
            }

            if (_entrySelector.Items.Count == 0)
            {
                _entrySelector.Items.Add("ManualStart");
            }

            _entrySelector.SelectedIndex = 0;
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

        private async Task BrowseRuntimeFlowAsync()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Flow runtime (*" + FlowFileExtensions.FlowRuntime + ")|*" + FlowFileExtensions.FlowRuntime + "|All files (*.*)|*.*";
                dialog.Title = "Load production runtime flow";
                dialog.InitialDirectory = GetSampleFlowDirectory();
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadRuntimeFlowAsync(dialog.FileName).ConfigureAwait(true);
                }
            }
        }

        private Task LoadRuntimeFlowAsync()
        {
            return LoadRuntimeFlowAsync(_requestedRuntimePath);
        }

        private async Task LoadRuntimeFlowAsync(string runtimePath)
        {
            if (_runner != null && _runner.IsRunning)
            {
                await _runner.StopAsync(CancellationToken.None).ConfigureAwait(true);
            }

            _runtimePath = string.IsNullOrWhiteSpace(runtimePath) ? ResolveRuntimePath() : runtimePath;
            _runtimeFlow = RuntimeFlowSerializer.Load(_runtimePath);
            var validation = new FlowValidator(_nodes).Validate(_runtimeFlow);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("Runtime validation failed: " + string.Join("; ", validation.Errors.Select(x => x.Code + " " + x.Message).ToArray()));
            }

            _eventSink.Clear();
            _runner = new FlowEngine(_nodes, _eventSink, _devices).CreateRunner(_runtimeFlow);
            _runtimeStatusValue.Text = _runtimeFlow.FlowName + " (" + _runtimeFlow.Nodes.Count + " nodes)";
            _runtimeFileValue.Text = _runtimePath;
            _runnerStateValue.Text = "Loaded";
            PopulateEntrySelector(_runtimeFlow);
            _requestedRuntimePath = _runtimePath;
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
            entryName = string.IsNullOrWhiteSpace(entryName) ? GetSelectedEntryName() : entryName;
            if (!_runner.IsRunning)
            {
                await _runner.StartAsync(CancellationToken.None).ConfigureAwait(true);
                _runnerStateValue.Text = "Running";
            }

            _lastToken = CreateProductionToken(entryName);

            await _runner.TriggerAsync(entryName, _lastToken, CancellationToken.None).ConfigureAwait(true);
            RefreshTokenSummary(entryName);
        }

        private string GetSelectedEntryName()
        {
            if (_entrySelector != null && _entrySelector.SelectedItem != null)
            {
                return Convert.ToString(_entrySelector.SelectedItem, CultureInfo.InvariantCulture);
            }

            return "ManualStart";
        }

        private FlowToken CreateProductionToken(string entryName)
        {
            var token = new FlowToken
            {
                TokenId = Guid.NewGuid().ToString("N"),
                ProductId = "DemoProduct",
                WorkpieceId = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                PositionId = entryName
            };

            if (!string.IsNullOrWhiteSpace(entryName) &&
                entryName.StartsWith("Position", StringComparison.OrdinalIgnoreCase))
            {
                var shotIndex = entryName.IndexOf("2", StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : 1;
                if (shotIndex == 1 || string.IsNullOrWhiteSpace(_activeCaptureGroupId))
                {
                    _activeCaptureGroupId = "capture-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
                }

                var frame = CreateDemoFrame(_activeCaptureGroupId, shotIndex);
                token.CaptureGroupId = _activeCaptureGroupId;
                token.FrameId = frame.FrameId;
                token.Set("ShotIndex", shotIndex);
                token.Set("Frame", frame);
                token.Set("Image", frame.Image);
            }

            if (string.Equals(entryName, "CameraFrameStream", StringComparison.OrdinalIgnoreCase))
            {
                if (_scanFrameIndex == 0 || string.IsNullOrWhiteSpace(_activeScanGroupId))
                {
                    _activeScanGroupId = "scan-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
                }

                var image = new FakeVisionImage(
                    "scan-frame-" + _activeScanGroupId + "-" + _scanFrameIndex.ToString(CultureInfo.InvariantCulture),
                    640,
                    160,
                    "Mono8",
                    null);
                token.ScanGroupId = _activeScanGroupId;
                token.FrameId = image.ImageId;
                token.Set("FrameIndex", _scanFrameIndex);
                token.Set("Image", image);
                _scanFrameIndex = (_scanFrameIndex + 1) % 3;
            }

            return token;
        }

        private static CameraFrameData CreateDemoFrame(string captureGroupId, int shotIndex)
        {
            var image = new FakeVisionImage(
                "position-frame-" + captureGroupId + "-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                320,
                240,
                "Mono8",
                null);
            var frame = new CameraFrameData
            {
                CameraId = "Camera01",
                TriggerId = "position-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                FrameId = image.ImageId,
                GrabTime = DateTime.UtcNow,
                Image = image
            };
            frame.Metadata[FlowMetadataKeys.CaptureGroupId] = captureGroupId;
            frame.Metadata[FlowMetadataKeys.ShotIndex] = shotIndex;
            return frame;
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
                var candidate = Path.Combine(root, "samples", "flows", "single-shot" + FlowFileExtensions.FlowRuntime);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(root);
                root = parent == null ? null : parent.FullName;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\samples\flows\single-shot" + FlowFileExtensions.FlowRuntime));
        }

        private static string GetSampleFlowDirectory()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            for (var depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(root); depth++)
            {
                var candidate = Path.Combine(root, "samples", "flows");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(root);
                root = parent == null ? null : parent.FullName;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
