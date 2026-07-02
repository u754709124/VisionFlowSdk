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
    // 运行辅助方法负责编译、发布、调试运行，并将 FlowRunner 事件回传到设计器。
    public sealed partial class FlowDesignerControl
    {
        private void OpenDesign()
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Open skipped: switch to Edit mode first.");
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Flow design (*" + FlowFileExtensions.FlowDesign + ")|*" + FlowFileExtensions.FlowDesign + "|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory()
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _document = FlowDesignSerializer.Load(dialog.FileName);
                if (_document.View == null)
                {
                    _document.View = new FlowViewState();
                }

                if (_document.Runtime == null)
                {
                    _document.Runtime = new RuntimeFlowDefinition();
                }

                _selectedNode = _document.Runtime.Nodes.FirstOrDefault();
                _selectedEdge = null;
                RenderCanvas();
                ApplyCanvasViewState();
                RenderProperties();
                AddDebugMessage("Opened " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Open failed: " + ex.Message);
            }
        }

        private void SaveDesign()
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Save skipped: switch to Edit mode first.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Flow design (*" + FlowFileExtensions.FlowDesign + ")|*" + FlowFileExtensions.FlowDesign + "|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory(),
                FileName = (_document.FlowId ?? "designer-flow") + FlowFileExtensions.FlowDesign
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                SaveCanvasViewState();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dialog.FileName));
                FlowDesignSerializer.Save(dialog.FileName, _document);
                AddDebugMessage("Saved design " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Save failed: " + ex.Message);
            }
        }

        private void PublishRuntime()
        {
            if (!CanEditDocument)
            {
                AddDebugMessage("Publish skipped: switch to Edit mode first.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Flow runtime (*" + FlowFileExtensions.FlowRuntime + ")|*" + FlowFileExtensions.FlowRuntime + "|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory(),
                FileName = (_document.Runtime.FlowId ?? "designer-flow") + FlowFileExtensions.FlowRuntime
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var publishResult = new FlowPublishService(_nodeRegistry).Publish(_document);
                if (!publishResult.IsSuccess)
                {
                    AddDebugMessage("Publish validation failed: " + FormatValidationIssues(publishResult.Validation));
                    return;
                }

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dialog.FileName));
                RuntimeFlowSerializer.Save(dialog.FileName, publishResult.Runtime);
                AddDebugMessage("Published runtime " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Publish failed: " + ex.Message);
            }
        }

        private async Task RunDebugAsync()
        {
            if (!IsDebugRunMode)
            {
                await SetInteractionModeAsync(DesignerInteractionMode.DebugRun).ConfigureAwait(true);
            }

            if (_document == null || _document.Runtime == null || _document.Runtime.Nodes.Count == 0)
            {
                AddDebugMessage("Debug skipped: the flow has no nodes.");
                return;
            }

            try
            {
                await StopDebugAsync().ConfigureAwait(true);
                ResetNodeStates();

                var sink = new DesignerEventSink(this);
                var engine = new FlowEngine(_nodeRegistry, sink, DebugDevices);
                var runner = engine.CreateRunner(_document.Runtime);
                _runner = runner;
                _isDebugRunning = true;
                UpdateInteractionModeUi();

                await runner.StartAsync().ConfigureAwait(true);
                await runner.TriggerAsync(DefaultEntryName, CreateDebugToken()).ConfigureAwait(true);
                AddDebugMessage("Debug run completed.");
            }
            catch (OperationCanceledException)
            {
                AddDebugMessage("Debug run stopped.");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Debug run failed: " + ex.Message);
            }
            finally
            {
                var runner = _runner;
                if (runner != null)
                {
                    try
                    {
                        if (runner.IsRunning)
                        {
                            await runner.StopAsync().ConfigureAwait(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddDebugMessage("Stop failed: " + ex.Message);
                    }
                }

                _runner = null;
                _isDebugRunning = false;
                UpdateInteractionModeUi();
            }
        }

        private async Task StopDebugAsync()
        {
            if (_runner == null)
            {
                _isDebugRunning = false;
                UpdateInteractionModeUi();
                return;
            }

            try
            {
                if (_runner.IsRunning)
                {
                    await _runner.StopAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                AddDebugMessage("Stop failed: " + ex.Message);
            }
            finally
            {
                _runner = null;
                _isDebugRunning = false;
                MarkRunningNodeStatesStopped();
                UpdateInteractionModeUi();
            }
        }

        private void ResetNodeStates()
        {
            _nodeStartTimes.Clear();
            foreach (var card in _nodeCards.Values)
            {
                card.SetRuntimeState(NodeRuntimeState.Waiting);
            }
        }

        private void ClearNodeRuntimeStates()
        {
            _nodeStartTimes.Clear();
            foreach (var card in _nodeCards.Values)
            {
                card.ClearRuntimeState();
            }
        }

        private void MarkRunningNodeStatesStopped()
        {
            var now = DateTime.UtcNow;
            foreach (var item in _nodeCards)
            {
                var elapsed = default(TimeSpan?);
                DateTime started;
                if (_nodeStartTimes.TryGetValue(item.Key, out started))
                {
                    elapsed = now - started;
                }

                item.Value.StopRunningRuntimeState(elapsed, "Stopped by user.");
            }

            _nodeStartTimes.Clear();
        }

        private async Task SetInteractionModeAsync(DesignerInteractionMode mode)
        {
            if (_interactionMode == mode)
            {
                return;
            }

            CancelConnectionPreview();
            if (mode == DesignerInteractionMode.Edit)
            {
                await StopDebugAsync().ConfigureAwait(true);
                _interactionMode = mode;
                ClearNodeRuntimeStates();
            }
            else
            {
                _interactionMode = mode;
                ResetNodeStates();
            }

            UpdateInteractionModeUi();
            RenderProperties();
            UpdateStatus();
        }

        private void UpdateInteractionModeUi()
        {
            var isEdit = CanEditDocument;
            _palette.SetReadOnly(!isEdit);
            _edges.SetReadOnly(!isEdit);

            foreach (var item in _nodeCards)
            {
                item.Value.SetEditEnabled(isEdit);
                item.Value.ContextMenu = isEdit ? CreateNodeContextMenu(item.Value.ViewModel.Node) : null;
            }

            SetToolbarButtonEnabled(_newButton, isEdit);
            SetToolbarButtonEnabled(_sampleButton, isEdit);
            SetToolbarButtonEnabled(_openButton, isEdit);
            SetToolbarButtonEnabled(_saveButton, isEdit);
            SetToolbarButtonEnabled(_publishButton, isEdit);
            SetToolbarButtonEnabled(_debugRunButton, IsDebugRunMode && !_isDebugRunning);
            SetToolbarButtonEnabled(_stopButton, IsDebugRunMode && _isDebugRunning);
            ApplyModeButtonStyle(_editModeButton, isEdit);
            ApplyModeButtonStyle(_debugModeButton, IsDebugRunMode);
        }

        private static void SetToolbarButtonEnabled(Button button, bool isEnabled)
        {
            if (button != null)
            {
                button.IsEnabled = isEnabled;
                button.Opacity = isEnabled ? 1.0 : 0.48;
            }
        }

        private static void ApplyModeButtonStyle(Button button, bool isActive)
        {
            if (button == null)
            {
                return;
            }

            button.Background = isActive
                ? BrushFromRgb(16, 185, 129)
                : BrushFromRgb(30, 41, 59);
            button.BorderBrush = isActive
                ? BrushFromRgb(52, 211, 153)
                : BrushFromRgb(51, 65, 85);
            button.Foreground = Brushes.White;
            button.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private FlowToken CreateDebugToken()
        {
            return new FlowToken
            {
                ProductId = "DemoProduct",
                WorkpieceId = "WP-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture),
                PositionId = "P1",
                CaptureGroupId = "SingleShot"
            };
        }

        private void HandleRuntimeEvent(FlowRuntimeEvent runtimeEvent)
        {
            if (runtimeEvent == null)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { HandleRuntimeEvent(runtimeEvent); }));
                return;
            }

            _debug.AddEvent(runtimeEvent);
            if (!IsDebugRunMode)
            {
                return;
            }

            if (runtimeEvent.EventType == FlowRuntimeEventType.FlowStopped)
            {
                MarkRunningNodeStatesStopped();
                _isDebugRunning = false;
                UpdateInteractionModeUi();
                return;
            }

            if (!string.IsNullOrWhiteSpace(runtimeEvent.NodeId) && _nodeCards.ContainsKey(runtimeEvent.NodeId))
            {
                if (!IsNodeLifecycleRuntimeEvent(runtimeEvent.EventType))
                {
                    return;
                }

                if (runtimeEvent.EventType == FlowRuntimeEventType.NodeStarted)
                {
                    _nodeStartTimes[runtimeEvent.NodeId] = DateTime.UtcNow;
                }

                TimeSpan? elapsed = null;
                DateTime started;
                if ((runtimeEvent.EventType == FlowRuntimeEventType.NodeCompleted ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeFailed ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeTimeout) &&
                    runtimeEvent.ElapsedMs >= 0)
                {
                    elapsed = TimeSpan.FromMilliseconds(runtimeEvent.ElapsedMs);
                }
                else if ((runtimeEvent.EventType == FlowRuntimeEventType.NodeCompleted ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeFailed ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeTimeout) &&
                    _nodeStartTimes.TryGetValue(runtimeEvent.NodeId, out started))
                {
                    elapsed = DateTime.UtcNow - started;
                }

                _nodeCards[runtimeEvent.NodeId].SetRuntimeState(runtimeEvent.State, elapsed, runtimeEvent.Message);
            }
        }

        private static bool IsNodeLifecycleRuntimeEvent(FlowRuntimeEventType eventType)
        {
            return eventType == FlowRuntimeEventType.NodeStarted ||
                eventType == FlowRuntimeEventType.NodeCompleted ||
                eventType == FlowRuntimeEventType.NodeFailed ||
                eventType == FlowRuntimeEventType.NodeTimeout;
        }

        private void AddDebugMessage(string message)
        {
            _debug.AddMessage(message);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            var nodeCount = _document == null || _document.Runtime == null ? 0 : _document.Runtime.Nodes.Count;
            var edgeCount = _document == null || _document.Runtime == null ? 0 : _document.Runtime.Edges.Count;
            var selected = _selectedNode != null
                ? _selectedNode.Id
                : (_selectedEdge == null ? "none" : FormatEdgeLabel(_selectedEdge));
            var zoom = _canvasScale == null ? 1.0 : _canvasScale.ScaleX;
            var mode = CanEditDocument ? "Edit" : "Debug Run";
            _statusText.Text = string.Format(CultureInfo.InvariantCulture, "{0} nodes | {1} edges | zoom {2:P0} | mode: {3} | selected: {4}", nodeCount, edgeCount, zoom, mode, selected);
        }

        private static string FormatValidationIssues(FlowValidationResult validation)
        {
            if (validation == null || validation.Issues.Count == 0)
            {
                return "unknown validation failure.";
            }

            var parts = validation.Issues
                .Where(x => x.Severity == FlowValidationSeverity.Error)
                .Take(4)
                .Select(x => string.IsNullOrWhiteSpace(x.NodeId)
                    ? x.Code + ": " + x.Message
                    : x.Code + " [" + x.NodeId + "]: " + x.Message)
                .ToArray();
            return string.Join("; ", parts);
        }

        private NodeDescriptor GetDescriptor(string nodeType)
        {
            INodeFactory factory;
            return _nodeRegistry.TryGetFactory(nodeType, out factory) ? factory.Descriptor : null;
        }

        private string CreateNodeId(string nodeType)
        {
            var prefix = string.IsNullOrWhiteSpace(nodeType) ? "node" : nodeType.Replace('.', '_').Replace('-', '_');
            var index = 1;
            string id;
            do
            {
                id = prefix + "_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }
            while (_document.Runtime.Nodes.Any(x => StringEquals(x.Id, id)));

            return id;
        }

        private sealed class DesignerEventSink : IFlowEventSink
        {
            private readonly FlowDesignerControl _owner;

            public DesignerEventSink(FlowDesignerControl owner)
            {
                _owner = owner;
            }

            public Task PublishAsync(FlowRuntimeEvent runtimeEvent, System.Threading.CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _owner.HandleRuntimeEvent(runtimeEvent);
                return Task.FromResult(0);
            }
        }
    }
}
