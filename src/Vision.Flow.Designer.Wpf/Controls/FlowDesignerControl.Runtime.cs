using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 文档命令负责独立设计器的打开、保存、发布和状态反馈。
    public sealed partial class FlowDesignerControl
    {
        private void OpenDesign()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Flow design (*" + FlowFileExtensions.FlowDesign + ")|*" + FlowFileExtensions.FlowDesign + "|All files (*.*)|*.*",
                InitialDirectory = GetSampleFlowDirectory()
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!TryResolvePendingPropertyChanges())
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
                BeginPropertyDraft(_selectedNode);
                _selectedEdge = null;
                RenderCanvas();
                ApplyCanvasViewState();
                RenderProperties();
                UpdateStatusMessage("Opened " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                UpdateStatusMessage("Open failed: " + ex.Message);
            }
        }

        private void SaveDesign()
        {
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

            if (!TryResolvePendingPropertyChanges())
            {
                return;
            }

            try
            {
                SaveCanvasViewState();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dialog.FileName));
                FlowDesignSerializer.Save(dialog.FileName, _document);
                UpdateStatusMessage("Saved design " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                UpdateStatusMessage("Save failed: " + ex.Message);
            }
        }

        private void ShowPublishRuntimeDialog()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Flow runtime (*" + FlowFileExtensions.FlowRuntime + ")|*" + FlowFileExtensions.FlowRuntime + "|All files (*.*)|*.*",
                DefaultExt = FlowFileExtensions.FlowRuntime,
                AddExtension = true,
                InitialDirectory = GetSampleFlowDirectory(),
                FileName = (_document.Runtime.FlowId ?? "designer-flow") + FlowFileExtensions.FlowRuntime
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (!TryResolvePendingPropertyChanges())
            {
                return;
            }

            try
            {
                var publishResult = PublishRuntimeFile(dialog.FileName);
                if (!publishResult.IsSuccess)
                {
                    UpdateStatusMessage("Publish validation failed: " + FormatValidationIssues(publishResult.Validation));
                    return;
                }

                UpdateStatusMessage("Published runtime " + dialog.FileName + ".");
            }
            catch (Exception ex)
            {
                UpdateStatusMessage("Publish failed: " + ex.Message);
            }
        }

        private void UpdateStatusMessage(string message)
        {
            _statusText.Text = message ?? string.Empty;
        }

        private void UpdateStatus()
        {
            var nodeCount = _document == null || _document.Runtime == null ? 0 : _document.Runtime.Nodes.Count;
            var edgeCount = _document == null || _document.Runtime == null ? 0 : _document.Runtime.Edges.Count;
            var selected = _selectedNode != null
                ? _selectedNode.Id
                : (_selectedEdge == null ? "none" : FormatEdgeLabel(_selectedEdge));
            var zoom = _canvasScale == null ? 1.0 : _canvasScale.ScaleX;
            _statusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} nodes | {1} edges | zoom {2:P0} | selected: {3}",
                nodeCount,
                edgeCount,
                zoom,
                selected);
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

        private NodeDescriptor GetDescriptor(NodeDefinition node)
        {
            NodeDescriptor descriptor;
            TryResolveDescriptor(node, out descriptor);
            return descriptor;
        }

        private bool TryResolveDescriptor(NodeDefinition node, out NodeDescriptor descriptor)
        {
            descriptor = null;
            if (node == null)
            {
                return false;
            }

            try
            {
                descriptor = _nodeRegistry.ResolveDescriptor(
                    _document.Runtime,
                    node);
                if (descriptor != null)
                {
                    return true;
                }
            }
            catch
            {
                // 动态描述符错误由校验器生成结构化问题；设计器退回静态描述符以保持界面可编辑。
            }

            descriptor = GetDescriptor(node.Type);
            return false;
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

    }
}
