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
    // Runtime helpers compile, publish, debug-run, and surface FlowRunner events back to the designer.
    public sealed partial class FlowDesignerControl
    {
        private void OpenDesign()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Flow design (*.flowdesign)|*.flowdesign|All files (*.*)|*.*",
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
            var dialog = new SaveFileDialog
            {
                Filter = "Flow design (*.flowdesign)|*.flowdesign|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory(),
                FileName = (_document.FlowId ?? "designer-flow") + ".flowdesign"
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
            var dialog = new SaveFileDialog
            {
                Filter = "Flow runtime (*.flowruntime)|*.flowruntime|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory(),
                FileName = (_document.Runtime.FlowId ?? "designer-flow") + ".flowruntime"
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
                _runner = engine.CreateRunner(_document.Runtime);
                await _runner.StartAsync().ConfigureAwait(true);
                await _runner.TriggerAsync(DefaultEntryName, CreateDebugToken()).ConfigureAwait(true);
                await _runner.StopAsync().ConfigureAwait(true);
                AddDebugMessage("Debug run completed.");
            }
            catch (Exception ex)
            {
                AddDebugMessage("Debug run failed: " + ex.Message);
            }
        }

        private async Task StopDebugAsync()
        {
            if (_runner == null)
            {
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
            if (!string.IsNullOrWhiteSpace(runtimeEvent.NodeId) && _nodeCards.ContainsKey(runtimeEvent.NodeId))
            {
                if (runtimeEvent.EventType == FlowRuntimeEventType.NodeStarted)
                {
                    _nodeStartTimes[runtimeEvent.NodeId] = DateTime.UtcNow;
                }

                TimeSpan? elapsed = null;
                DateTime started;
                if ((runtimeEvent.EventType == FlowRuntimeEventType.NodeCompleted ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeFailed ||
                    runtimeEvent.EventType == FlowRuntimeEventType.NodeTimeout) &&
                    _nodeStartTimes.TryGetValue(runtimeEvent.NodeId, out started))
                {
                    elapsed = DateTime.UtcNow - started;
                }

                _nodeCards[runtimeEvent.NodeId].SetRuntimeState(runtimeEvent.State, elapsed, runtimeEvent.Message);
            }
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
            _statusText.Text = string.Format(CultureInfo.InvariantCulture, "{0} nodes | {1} edges | zoom {2:P0} | selected: {3}", nodeCount, edgeCount, zoom, selected);
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
