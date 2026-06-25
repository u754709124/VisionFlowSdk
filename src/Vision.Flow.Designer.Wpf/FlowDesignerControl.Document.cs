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
    // Document helpers manage design templates, node editing, selection, and property refresh.
    public sealed partial class FlowDesignerControl
    {
        private void CreateNewDesign()
        {
            _document = CreateDocument("designer-flow", "Designer Flow");
            _selectedNode = null;
            _selectedEdge = null;
            RenderCanvas();
            ApplyCanvasViewState();
            RenderProperties();
            _debug.Clear();
            AddDebugMessage("New design created.");
        }

        private void LoadSingleShotTemplate()
        {
            _document = CreateDocument("designer-single-shot", "Single Shot Inspection");
            var flow = _document.Runtime;

            AddTemplateNode("light_1", "light.control", "Light Control", 70, 90, new Dictionary<string, object>
            {
                { "LightId", "Light01" },
                { "Channels", CreateLightChannels("Main", 85) },
                { "StableDelayMs", 0 }
            });
            AddTemplateNode("trigger_1", "camera.soft_trigger", "Camera Trigger", 360, 90, new Dictionary<string, object>
            {
                { "CameraId", "Camera01" },
                { "TimeoutMs", 1000 }
            });
            AddTemplateNode("callback_1", "camera.image_callback", "Image Callback", 650, 90, new Dictionary<string, object>
            {
                { "CameraId", "Camera01" },
                { "MatchMode", "TriggerId" },
                { "TimeoutMs", 1500 }
            });
            AddTemplateNode("recipe_1", "recipe.run", "Recipe Run", 940, 90, new Dictionary<string, object>
            {
                { "RecipeId", "Recipe01" },
                { "InputImageBinding", "{{ callback_1.Image }}" },
                { "TimeoutMs", 5000 }
            });
            AddTemplateNode("save_1", "image.save", "Save Image", 1230, 90, new Dictionary<string, object>
            {
                { "SaverId", "ImageSave01" },
                { "ImageBinding", "{{ callback_1.Image }}" },
                { "ResultImageBinding", "{{ recipe_1.ResultImage }}" },
                { "FileNameTemplate", "{TokenId}_{ImageId}.png" }
            });
            AddTemplateNode("db_1", "database.save", "Save Result", 1230, 300, new Dictionary<string, object>
            {
                { "DatabaseId", "VisionDb" },
                { "TableName", "InspectionResults" },
                { "FieldMappings", CreateFieldMappings("IsOk={{ recipe_1.IsOk }};FrameId={{ callback_1.FrameId }};ImagePath={{ save_1.ImagePath }}") }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = DefaultEntryName, TargetNodeId = "light_1" });
            AddEdge("light_1", "trigger_1");
            AddEdge("trigger_1", "callback_1");
            AddEdge("callback_1", "recipe_1");
            AddEdge("recipe_1", "save_1");
            AddEdge("save_1", "db_1");

            _selectedNode = flow.Nodes.FirstOrDefault();
            _selectedEdge = null;
            RenderCanvas();
            ApplyCanvasViewState();
            RenderProperties();
            _debug.Clear();
            AddDebugMessage("Single-shot sample loaded.");
        }

        private void AddTemplateNode(
            string nodeId,
            string nodeType,
            string nodeName,
            double x,
            double y,
            IDictionary<string, object> settings)
        {
            var descriptor = GetDescriptor(nodeType);
            var node = new NodeDefinition
            {
                Id = nodeId,
                Type = nodeType,
                Name = nodeName,
                Version = descriptor == null ? "1.0.0" : descriptor.Version
            };

            foreach (var setting in settings)
            {
                node.Settings[setting.Key] = setting.Value;
            }

            _document.Runtime.Nodes.Add(node);
            _document.View.Nodes[nodeId] = new NodeViewState { X = x, Y = y };
        }

        private FlowDesignDocument CreateDocument(string flowId, string flowName)
        {
            return new FlowDesignDocument
            {
                FlowId = flowId,
                FlowName = flowName,
                Runtime = new RuntimeFlowDefinition
                {
                    FlowId = flowId,
                    FlowName = flowName,
                    Version = "1.0.0"
                },
                View = new FlowViewState()
            };
        }

        private void AddNodeFromPalette(NodeDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return;
            }

            var previous = _document.Runtime.Nodes.LastOrDefault();
            var nodeId = CreateNodeId(descriptor.NodeType);
            var node = new NodeDefinition
            {
                Id = nodeId,
                Type = descriptor.NodeType,
                Name = descriptor.DisplayName,
                Version = descriptor.Version
            };

            foreach (var setting in descriptor.Settings)
            {
                node.Settings[setting.Name] = CreateDefaultSettingValue(setting);
            }

            AutoConfigureNode(node, previous);
            _document.Runtime.Nodes.Add(node);
            _document.View.Nodes[node.Id] = new NodeViewState
            {
                X = 80 + (_document.Runtime.Nodes.Count % 4) * 280,
                Y = 80 + (_document.Runtime.Nodes.Count / 4) * 170
            };

            if (_document.Runtime.Entries.Count == 0)
            {
                _document.Runtime.Entries.Add(new FlowEntryDefinition { EntryName = DefaultEntryName, TargetNodeId = node.Id });
            }

            if (previous != null)
            {
                AddEdge(previous.Id, node.Id);
            }

            SelectNode(node);
            RenderCanvas();
            AddDebugMessage("Added node " + node.Id + " (" + descriptor.NodeType + ").");
        }

        private void AutoConfigureNode(NodeDefinition node, NodeDefinition previous)
        {
            if (node == null)
            {
                return;
            }

            if (string.Equals(node.Type, "recipe.run", StringComparison.OrdinalIgnoreCase) &&
                previous != null &&
                string.Equals(previous.Type, "camera.image_callback", StringComparison.OrdinalIgnoreCase))
            {
                node.Settings["InputImageBinding"] = "{{ " + previous.Id + ".Image }}";
            }

            if (string.Equals(node.Type, "image.save", StringComparison.OrdinalIgnoreCase) && previous != null)
            {
                if (string.Equals(previous.Type, "camera.image_callback", StringComparison.OrdinalIgnoreCase))
                {
                    node.Settings["ImageBinding"] = "{{ " + previous.Id + ".Image }}";
                }

                if (string.Equals(previous.Type, "recipe.run", StringComparison.OrdinalIgnoreCase))
                {
                    node.Settings["ResultImageBinding"] = "{{ " + previous.Id + ".ResultImage }}";
                }
            }

            if (string.Equals(node.Type, "database.save", StringComparison.OrdinalIgnoreCase) && previous != null)
            {
                node.Settings["FieldMappings"] = CreateFieldMappings("PreviousNode=" + previous.Id);
            }
        }

        private object CreateDefaultSettingValue(NodeSettingDescriptor setting)
        {
            if (setting == null)
            {
                return null;
            }

            if (setting.DefaultValue != null)
            {
                return setting.DefaultValue;
            }

            if (StringEquals(setting.Name, "CameraId"))
            {
                return "Camera01";
            }

            if (StringEquals(setting.Name, "LightId"))
            {
                return "Light01";
            }

            if (StringEquals(setting.Name, "RecipeId"))
            {
                return "Recipe01";
            }

            if (StringEquals(setting.Name, "DatabaseId"))
            {
                return "VisionDb";
            }

            if (StringEquals(setting.Name, "TableName"))
            {
                return "InspectionResults";
            }

            if (StringEquals(setting.Name, "Channels"))
            {
                return CreateLightChannels("Main", 80);
            }

            if (StringEquals(setting.Name, "Parameters"))
            {
                return CreateCameraParameters("ExposureTime=1000;Gain=1");
            }

            if (StringEquals(setting.Name, "FieldMappings"))
            {
                return CreateFieldMappings("TokenId={{ token.TokenId }}");
            }

            if (StringEquals(setting.Name, "Message"))
            {
                return "Debug node executed.";
            }

            return null;
        }

        private void AddEdge(string fromNodeId, string toNodeId)
        {
            AddEdge(fromNodeId, "Next", toNodeId, "In");
        }

        private void AddEdge(string fromNodeId, string fromPort, string toNodeId, string toPort)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId))
            {
                return;
            }

            var exists = _document.Runtime.Edges.Any(x =>
                StringEquals(x.FromNodeId, fromNodeId) &&
                StringEquals(x.ToNodeId, toNodeId) &&
                StringEquals(x.FromPort, fromPort) &&
                StringEquals(x.ToPort, toPort));
            if (exists)
            {
                return;
            }

            _document.Runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = fromNodeId,
                FromPort = string.IsNullOrWhiteSpace(fromPort) ? "Next" : fromPort,
                ToNodeId = toNodeId,
                ToPort = string.IsNullOrWhiteSpace(toPort) ? "In" : toPort
            });
        }

        private void SelectEdge(EdgeDefinition edge)
        {
            if (edge == null)
            {
                return;
            }

            Focus();
            _selectedEdge = edge;
            _selectedNode = null;
            foreach (var item in _nodeCards)
            {
                item.Value.SetSelected(false);
            }

            RenderProperties();
            RenderEdges();
            UpdateStatus();
        }

        private void DeleteSelection()
        {
            if (_selectedEdge != null)
            {
                DeleteEdge(_selectedEdge);
                return;
            }

            if (_selectedNode != null)
            {
                DeleteNode(_selectedNode);
            }
        }

        private void DeleteEdge(EdgeDefinition edge)
        {
            if (edge == null || _document == null || _document.Runtime == null)
            {
                return;
            }

            CancelConnectionPreview();
            _document.Runtime.Edges.RemoveAll(x => object.ReferenceEquals(x, edge) || EdgeEquals(x, edge));
            if (EdgeEquals(_selectedEdge, edge))
            {
                _selectedEdge = null;
            }

            RenderEdges();
            UpdateStatus();
            AddDebugMessage("Deleted edge " + edge.FromNodeId + " -> " + edge.ToNodeId + ".");
        }

        private void RenderCanvas()
        {
            _nodeLayer.Children.Clear();
            _nodeCards.Clear();

            if (_document == null || _document.Runtime == null)
            {
                return;
            }

            foreach (var node in _document.Runtime.Nodes)
            {
                NodeViewState view;
                if (!_document.View.Nodes.TryGetValue(node.Id, out view))
                {
                    view = new NodeViewState { X = 80, Y = 80 };
                    _document.View.Nodes[node.Id] = view;
                }

                var descriptor = GetDescriptor(node.Type);
                var card = new NodeCardControl(new NodeViewModel(node, descriptor));
                card.SetSelected(StringEquals(node.Id, _selectedNode == null ? null : _selectedNode.Id));
                card.SetDisabled(IsNodeDisabled(node));
                card.MouseLeftButtonDown += OnNodeMouseDown;
                card.MouseMove += OnNodeMouseMove;
                card.MouseLeftButtonUp += OnNodeMouseUp;
                card.OutputPortDragStarted += OnOutputPortDragStarted;
                card.InputPortDragCompleted += OnInputPortDragCompleted;
                card.ContextMenu = CreateNodeContextMenu(node);
                Canvas.SetLeft(card, view.X);
                Canvas.SetTop(card, view.Y);
                _nodeLayer.Children.Add(card);
                _nodeCards[node.Id] = card;
            }

            _hasDeferredEdgeRefresh = true;
            RenderEdges();
            UpdateStatus();
        }

        private void RenderProperties()
        {
            _properties.ShowNode(
                _selectedNode,
                _selectedNode == null ? null : GetDescriptor(_selectedNode.Type),
                CreateVariableSuggestions(_selectedNode),
                delegate
                {
                    var card = _selectedNode == null || !_nodeCards.ContainsKey(_selectedNode.Id)
                        ? null
                        : _nodeCards[_selectedNode.Id];
                    if (card != null)
                    {
                        card.UpdateSummary();
                    }

                    RenderCanvas();
                });
        }

        private IList<string> CreateVariableSuggestions(NodeDefinition currentNode)
        {
            var items = new List<string>();
            AddTokenVariableSuggestions(items);

            if (_document == null || _document.Runtime == null || _document.Runtime.Nodes == null)
            {
                return items;
            }

            foreach (var node in _document.Runtime.Nodes)
            {
                if (node == null ||
                    string.IsNullOrWhiteSpace(node.Id) ||
                    (currentNode != null && StringEquals(node.Id, currentNode.Id)))
                {
                    continue;
                }

                var descriptor = GetDescriptor(node.Type);
                if (descriptor == null || descriptor.Outputs == null)
                {
                    continue;
                }

                foreach (var output in descriptor.Outputs)
                {
                    if (!string.IsNullOrWhiteSpace(output.Name))
                    {
                        AddVariableSuggestion(items, node.Id, output.Name);
                    }
                }
            }

            return items
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.StartsWith("{{ token.", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddTokenVariableSuggestions(ICollection<string> items)
        {
            AddVariableSuggestion(items, "token", "TokenId");
            AddVariableSuggestion(items, "token", "ProductId");
            AddVariableSuggestion(items, "token", "WorkpieceId");
            AddVariableSuggestion(items, "token", "PositionId");
            AddVariableSuggestion(items, "token", "CaptureGroupId");
            AddVariableSuggestion(items, "token", "ScanGroupId");
            AddVariableSuggestion(items, "token", "TriggerId");
            AddVariableSuggestion(items, "token", "FrameId");
            AddVariableSuggestion(items, "token", "ShotIndex");
            AddVariableSuggestion(items, "token", "FrameIndex");
            AddVariableSuggestion(items, "token", "Image");
            AddVariableSuggestion(items, "token", "Frame");
        }

        private static void AddVariableSuggestion(ICollection<string> items, string scope, string name)
        {
            if (items == null || string.IsNullOrWhiteSpace(scope) || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            items.Add("{{ " + scope + "." + name + " }}");
        }

        private void SelectNode(NodeDefinition node)
        {
            _selectedNode = node;
            _selectedEdge = null;
            foreach (var item in _nodeCards)
            {
                item.Value.SetSelected(StringEquals(item.Key, node == null ? null : node.Id));
            }

            RenderEdges();
            RenderProperties();
            UpdateStatus();
        }

        private void SelectNodeById(string nodeId)
        {
            if (_document == null || _document.Runtime == null || string.IsNullOrWhiteSpace(nodeId))
            {
                return;
            }

            var node = _document.Runtime.Nodes.FirstOrDefault(x => StringEquals(x.Id, nodeId));
            if (node == null)
            {
                return;
            }

            SelectNode(node);
            NodeViewState view;
            if (_document.View != null && _document.View.Nodes.TryGetValue(node.Id, out view) && _canvasScroll != null)
            {
                _canvasScroll.ScrollToHorizontalOffset(Math.Max(0, view.X - 80));
                _canvasScroll.ScrollToVerticalOffset(Math.Max(0, view.Y - 80));
            }
        }

        private ContextMenu CreateNodeContextMenu(NodeDefinition node)
        {
            var menu = new ContextMenu();
            menu.Items.Add(CreateMenuItem("Rename", delegate { RenameNode(node); }));
            menu.Items.Add(CreateMenuItem("Duplicate", delegate { DuplicateNode(node); }));
            menu.Items.Add(CreateMenuItem(IsNodeDisabled(node) ? "Enable" : "Disable", delegate { ToggleNodeDisabled(node); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("Delete", delegate { DeleteNode(node); }));
            return menu;
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler click)
        {
            var item = new MenuItem { Header = header };
            item.Click += click;
            return item;
        }

        private void RenameNode(NodeDefinition node)
        {
            if (node == null)
            {
                return;
            }

            var dialog = new Window
            {
                Title = "Rename Node",
                Width = 360,
                Height = 150,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this)
            };

            var layout = new StackPanel { Margin = new Thickness(16) };
            var textBox = new TextBox
            {
                Text = node.Name,
                MinHeight = 28,
                Padding = new Thickness(6, 4, 6, 4)
            };
            layout.Children.Add(textBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var ok = new Button { Content = "OK", MinWidth = 70, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", MinWidth = 70, Height = 28 };
            ok.Click += delegate { dialog.DialogResult = true; };
            cancel.Click += delegate { dialog.DialogResult = false; };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            layout.Children.Add(buttons);
            dialog.Content = layout;

            if (dialog.ShowDialog() == true)
            {
                node.Name = string.IsNullOrWhiteSpace(textBox.Text) ? node.Id : textBox.Text.Trim();
                RenderCanvas();
                SelectNode(node);
            }
        }

        private void DuplicateNode(NodeDefinition node)
        {
            if (node == null || _document == null || _document.Runtime == null)
            {
                return;
            }

            var clone = new NodeDefinition
            {
                Id = CreateNodeId(node.Type),
                Type = node.Type,
                Name = (string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name) + " Copy",
                Version = node.Version
            };

            foreach (var setting in node.Settings)
            {
                clone.Settings[setting.Key] = setting.Value;
            }

            foreach (var binding in node.InputBindings)
            {
                clone.InputBindings[binding.Key] = CloneBinding(binding.Value);
            }

            _document.Runtime.Nodes.Add(clone);
            NodeViewState view;
            if (!_document.View.Nodes.TryGetValue(node.Id, out view))
            {
                view = new NodeViewState { X = 80, Y = 80 };
            }

            _document.View.Nodes[clone.Id] = new NodeViewState
            {
                X = SnapToGrid(view.X + 48),
                Y = SnapToGrid(view.Y + 48)
            };
            RenderCanvas();
            SelectNode(clone);
            AddDebugMessage("Duplicated node " + node.Id + " as " + clone.Id + ".");
        }

        private void DeleteNode(NodeDefinition node)
        {
            if (node == null || _document == null || _document.Runtime == null)
            {
                return;
            }

            _document.Runtime.Nodes.Remove(node);
            _document.Runtime.Edges.RemoveAll(x => StringEquals(x.FromNodeId, node.Id) || StringEquals(x.ToNodeId, node.Id));
            _selectedEdge = null;
            if (_document.View != null)
            {
                _document.View.Nodes.Remove(node.Id);
            }

            if (_selectedNode == node)
            {
                _selectedNode = _document.Runtime.Nodes.FirstOrDefault();
            }

            RenderCanvas();
            RenderProperties();
            AddDebugMessage("Deleted node " + node.Id + ".");
        }

        private void ToggleNodeDisabled(NodeDefinition node)
        {
            if (node == null)
            {
                return;
            }

            node.Settings["Disabled"] = !IsNodeDisabled(node);
            RenderCanvas();
            SelectNode(node);
        }

        private static bool IsNodeDisabled(NodeDefinition node)
        {
            object value;
            return node != null &&
                node.Settings != null &&
                node.Settings.TryGetValue("Disabled", out value) &&
                value != null &&
                Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static VariableBinding CloneBinding(VariableBinding source)
        {
            if (source == null)
            {
                return null;
            }

            return new VariableBinding
            {
                Expression = source.Expression,
                SourceNodeId = source.SourceNodeId,
                SourceOutputName = source.SourceOutputName,
                ConstantValue = source.ConstantValue,
                ValueType = source.ValueType,
                IsConstant = source.IsConstant
            };
        }
    }
}
