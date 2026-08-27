using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Designer.Wpf.Theming;
using Vision.Flow.Designer.Wpf.ViewModels;
using Vision.Flow.Nodes;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 属性草稿只覆盖节点名称、配置和执行策略，不改变稳定节点协议标识。
    public sealed partial class FlowDesignerControl
    {
        /// <summary>
        /// 当前属性面板是否包含尚未应用到流程文档的修改或非法输入。
        /// </summary>
        public bool HasPendingPropertyChanges
        {
            get
            {
                if (!Dispatcher.CheckAccess())
                {
                    return Dispatcher.Invoke(new Func<bool>(delegate { return HasPendingPropertyChanges; }));
                }

                return HasPendingPropertyChangesCore();
            }
        }

        /// <summary>
        /// 校验并一次性提交当前节点属性草稿。无草稿或无修改时同样返回 true。
        /// </summary>
        public bool TryApplyPendingPropertyChanges(out string error)
        {
            if (!Dispatcher.CheckAccess())
            {
                string dispatcherError = null;
                var result = Dispatcher.Invoke(new Func<bool>(delegate
                {
                    return TryApplyPendingPropertyChanges(out dispatcherError);
                }));
                error = dispatcherError;
                return result;
            }

            error = null;
            if (_selectedNode == null || _propertyDraftNode == null || !HasPendingPropertyChangesCore())
            {
                _properties.SetPendingState(false, false);
                return true;
            }

            if (!_properties.TryValidate(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "请修正属性面板中的输入错误。";
                }

                _properties.ShowValidationError(error);
                return false;
            }

            var snapshot = CloneDesignDocument(_document);
            var snapshotIndex = snapshot.Runtime.Nodes.FindIndex(x => StringEquals(x.Id, _selectedNode.Id));
            if (snapshotIndex >= 0)
            {
                snapshot.Runtime.Nodes[snapshotIndex] = CloneNodeDefinition(_propertyDraftNode);
                var validation = new FlowValidator(_nodeRegistry).Validate(snapshot);
                var nodeErrors = validation.Issues
                    .Where(x => x.Severity == FlowValidationSeverity.Error && StringEquals(x.NodeId, _selectedNode.Id))
                    .Take(4)
                    .ToList();
                if (nodeErrors.Count > 0)
                {
                    error = string.Join("；", nodeErrors.Select(x => x.Message).ToArray());
                    _properties.ShowValidationError(error);
                    _properties.FocusValidationSummary();
                    return false;
                }
            }

            CopyEditableNodeState(_propertyDraftNode, _selectedNode);
            _propertyBaselineNode = CloneNodeDefinition(_selectedNode);
            _propertyDraftNode = CloneNodeDefinition(_selectedNode);
            UpdatePropertyDraftDescriptorState();
            _properties.ResetEditorState();
            RenderCanvas();
            RenderProperties();
            _properties.ShowValidationError(null);
            UpdateStatusMessage("Applied properties for node " + _selectedNode.Id + ".");
            return true;
        }

        /// <summary>
        /// 放弃当前草稿并恢复最近一次已应用状态。
        /// </summary>
        public void DiscardPendingPropertyChanges()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(DiscardPendingPropertyChanges));
                return;
            }

            if (_selectedNode == null)
            {
                ClearPropertyDraft();
                RenderProperties();
                return;
            }

            _propertyBaselineNode = CloneNodeDefinition(_selectedNode);
            _propertyDraftNode = CloneNodeDefinition(_selectedNode);
            UpdatePropertyDraftDescriptorState();
            _properties.ResetEditorState();
            RenderProperties();
            _properties.ShowValidationError(null);
        }

        /// <summary>
        /// 解决未应用属性。成功应用或放弃后返回 true；取消或校验失败返回 false。
        /// </summary>
        public bool TryResolvePendingPropertyChanges()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(new Func<bool>(TryResolvePendingPropertyChanges));
            }

            if (!HasPendingPropertyChangesCore())
            {
                return true;
            }

            var decision = _options.PendingPropertyChangesPrompt == null
                ? ShowPendingPropertyChangesDialog()
                : _options.PendingPropertyChangesPrompt();
            switch (decision)
            {
                case PendingPropertyChangesDecision.Apply:
                    string error;
                    return TryApplyPendingPropertyChanges(out error);
                case PendingPropertyChangesDecision.Discard:
                    DiscardPendingPropertyChanges();
                    return true;
                default:
                    return false;
            }
        }

        private bool HasPendingPropertyChangesCore()
        {
            if (_selectedNode == null || _propertyDraftNode == null || _propertyBaselineNode == null)
            {
                return false;
            }

            return _properties.HasUnappliedEditorState ||
                !string.Equals(
                    SerializeComparableNode(_propertyBaselineNode),
                    SerializeComparableNode(_propertyDraftNode),
                    StringComparison.Ordinal);
        }

        private void BeginPropertyDraft(NodeDefinition node)
        {
            if (node == null)
            {
                ClearPropertyDraft();
                return;
            }

            _propertyBaselineNode = CloneNodeDefinition(node);
            _propertyDraftNode = CloneNodeDefinition(node);
            UpdatePropertyDraftDescriptorState();
            _properties.ResetEditorState();
        }

        private void ClearPropertyDraft()
        {
            _propertyBaselineNode = null;
            _propertyDraftNode = null;
            _propertyDraftDescriptor = null;
            _propertyDraftDescriptorState = null;
            _properties.ResetEditorState();
            _properties.SetPendingState(false, false);
            _properties.ShowValidationError(null);
        }

        private void OnPropertyDraftChanged()
        {
            _properties.ShowValidationError(null);
            ReconcilePropertyDraftDescriptor();
            _properties.SetPendingState(HasPendingPropertyChangesCore(), false);
        }

        private void UpdatePropertyDraftDescriptorState()
        {
            if (_propertyDraftNode == null)
            {
                _propertyDraftDescriptor = null;
                _propertyDraftDescriptorState = null;
                return;
            }

            _propertyDraftDescriptor = GetDescriptor(_propertyDraftNode);
            _propertyDraftDescriptorState = CreateDescriptorStateSignature(
                _propertyDraftNode,
                _propertyDraftDescriptor);
        }

        private void ReconcilePropertyDraftDescriptor()
        {
            if (_isReconcilingPropertyDescriptor || _propertyDraftNode == null)
            {
                return;
            }

            NodeDescriptor nextDescriptor;
            TryResolveDescriptor(_propertyDraftNode, out nextDescriptor);
            if (nextDescriptor == null)
            {
                return;
            }

            var nextState = CreateDescriptorStateSignature(_propertyDraftNode, nextDescriptor);
            if (string.Equals(_propertyDraftDescriptorState, nextState, StringComparison.Ordinal))
            {
                _propertyDraftDescriptor = nextDescriptor;
                return;
            }

            _isReconcilingPropertyDescriptor = true;
            try
            {
                var changedSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var changedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var previousDescriptor = _propertyDraftDescriptor;
                var currentDescriptor = nextDescriptor;

                for (var pass = 0; pass < 4; pass++)
                {
                    ReconcilePropertyDraftDefinition(
                        _propertyDraftNode,
                        previousDescriptor,
                        currentDescriptor,
                        changedSettings,
                        changedOutputs);

                    NodeDescriptor resolvedDescriptor;
                    if (!TryResolveDescriptor(_propertyDraftNode, out resolvedDescriptor))
                    {
                        break;
                    }

                    var reconciledDescriptor = currentDescriptor;
                    var currentState = CreateDescriptorStateSignature(_propertyDraftNode, reconciledDescriptor);
                    var resolvedState = CreateDescriptorStateSignature(_propertyDraftNode, resolvedDescriptor);
                    currentDescriptor = resolvedDescriptor;
                    if (string.Equals(currentState, resolvedState, StringComparison.Ordinal))
                    {
                        break;
                    }

                    previousDescriptor = reconciledDescriptor;
                }

                _properties.RemoveDescriptorEditorState(changedSettings, changedOutputs);
                _propertyDraftDescriptor = currentDescriptor;
                _propertyDraftDescriptorState = CreateDescriptorStateSignature(
                    _propertyDraftNode,
                    currentDescriptor);
                RenderProperties();
            }
            finally
            {
                _isReconcilingPropertyDescriptor = false;
            }
        }

        private static void ReconcilePropertyDraftDefinition(
            NodeDefinition node,
            NodeDescriptor previousDescriptor,
            NodeDescriptor nextDescriptor,
            ISet<string> changedSettings,
            ISet<string> changedOutputs)
        {
            var previousSettings = CreateSettingDescriptorMap(previousDescriptor);
            var nextSettings = CreateSettingDescriptorMap(nextDescriptor);
            foreach (var setting in previousSettings.Values)
            {
                NodeSettingDescriptor nextSetting;
                if (!nextSettings.TryGetValue(setting.Name, out nextSetting) ||
                    !AreSettingContractsEquivalent(setting, nextSetting))
                {
                    changedSettings.Add(setting.Name);
                }
            }

            foreach (var name in node.Settings.Keys.ToList())
            {
                if (StringEquals(name, FlowSettingNames.Disabled))
                {
                    continue;
                }

                NodeSettingDescriptor previousSetting;
                NodeSettingDescriptor nextSetting;
                if (!previousSettings.TryGetValue(name, out previousSetting) ||
                    !nextSettings.TryGetValue(name, out nextSetting) ||
                    !AreSettingContractsEquivalent(previousSetting, nextSetting))
                {
                    node.Settings.Remove(name);
                    changedSettings.Add(name);
                }
            }

            foreach (var setting in nextSettings.Values)
            {
                NodeSettingValue value;
                if (!node.Settings.TryGetValue(setting.Name, out value) || value == null)
                {
                    node.Settings[setting.Name] = NodeSettingValue.ForConstant(
                        CloneSettingConstantValue(setting.DefaultValue));
                    changedSettings.Add(setting.Name);
                }
            }

            var policy = node.ExecutionPolicy ?? new NodeExecutionPolicy();
            node.ExecutionPolicy = policy;
            var defaults = policy.DefaultOutputs;
            var previousOutputs = CreateOutputDescriptorMap(previousDescriptor);
            var nextOutputs = CreateOutputDescriptorMap(nextDescriptor);
            foreach (var output in previousOutputs.Values)
            {
                NodeOutputDescriptor nextOutput;
                if (!nextOutputs.TryGetValue(output.Name, out nextOutput) ||
                    output.DataType != nextOutput.DataType)
                {
                    changedOutputs.Add(output.Name);
                }
            }

            if (defaults != null)
            {
                foreach (var name in defaults.Keys.ToList())
                {
                    NodeOutputDescriptor previousOutput;
                    NodeOutputDescriptor nextOutput;
                    if (!previousOutputs.TryGetValue(name, out previousOutput) ||
                        !nextOutputs.TryGetValue(name, out nextOutput) ||
                        previousOutput.DataType != nextOutput.DataType)
                    {
                        defaults.Remove(name);
                        changedOutputs.Add(name);
                    }
                }
            }

            if (policy.FailureStrategy != FailureStrategy.DefaultOutputs)
            {
                return;
            }

            if (defaults == null)
            {
                defaults = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                policy.DefaultOutputs = defaults;
            }

            foreach (var output in nextOutputs.Values)
            {
                if (defaults.ContainsKey(output.Name))
                {
                    continue;
                }

                object defaultValue;
                if (TryCreateDefaultOutputValue(output.DataType, out defaultValue))
                {
                    defaults[output.Name] = defaultValue;
                    changedOutputs.Add(output.Name);
                }
            }
        }

        private static Dictionary<string, NodeSettingDescriptor> CreateSettingDescriptorMap(NodeDescriptor descriptor)
        {
            var result = new Dictionary<string, NodeSettingDescriptor>(StringComparer.OrdinalIgnoreCase);
            if (descriptor == null || descriptor.Settings == null)
            {
                return result;
            }

            foreach (var setting in descriptor.Settings.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                if (!result.ContainsKey(setting.Name))
                {
                    result[setting.Name] = setting;
                }
            }

            return result;
        }

        private static Dictionary<string, NodeOutputDescriptor> CreateOutputDescriptorMap(NodeDescriptor descriptor)
        {
            var result = new Dictionary<string, NodeOutputDescriptor>(StringComparer.OrdinalIgnoreCase);
            if (descriptor == null || descriptor.Outputs == null)
            {
                return result;
            }

            foreach (var output in descriptor.Outputs.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                if (!result.ContainsKey(output.Name))
                {
                    result[output.Name] = output;
                }
            }

            return result;
        }

        private static bool AreSettingContractsEquivalent(
            NodeSettingDescriptor left,
            NodeSettingDescriptor right)
        {
            return left != null &&
                right != null &&
                left.DataType == right.DataType &&
                left.EnumType == right.EnumType &&
                left.ObjectType == right.ObjectType &&
                left.BindingMode == right.BindingMode &&
                left.EvaluationPhase == right.EvaluationPhase &&
                left.AllowedVariableSources == right.AllowedVariableSources &&
                left.AffectsDescriptor == right.AffectsDescriptor &&
                Equals(left.VariableTypeValidator, right.VariableTypeValidator);
        }

        private static bool TryCreateDefaultOutputValue(FlowDataType dataType, out object value)
        {
            switch (dataType)
            {
                case FlowDataType.String:
                    value = string.Empty;
                    return true;
                case FlowDataType.Int32:
                    value = 0;
                    return true;
                case FlowDataType.Int64:
                    value = 0L;
                    return true;
                case FlowDataType.Boolean:
                    value = false;
                    return true;
                case FlowDataType.Double:
                    value = 0.0d;
                    return true;
                case FlowDataType.DateTime:
                    value = DateTime.MinValue;
                    return true;
                case FlowDataType.Object:
                    value = null;
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        private static string CreateDescriptorStateSignature(
            NodeDefinition node,
            NodeDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return null;
            }

            var result = new StringBuilder();
            AppendDescriptorValue(result, descriptor.NodeType);
            AppendDescriptorValue(result, descriptor.DisplayName);
            AppendDescriptorValue(result, descriptor.Category);
            AppendDescriptorValue(result, descriptor.Version);
            AppendDescriptorValue(result, descriptor.Description);

            foreach (var port in descriptor.InputPorts ?? new List<NodePortDescriptor>())
            {
                AppendPortSignature(result, "I", port);
            }

            foreach (var port in descriptor.OutputPorts ?? new List<NodePortDescriptor>())
            {
                AppendPortSignature(result, "O", port);
            }

            foreach (var setting in descriptor.Settings ?? new List<NodeSettingDescriptor>())
            {
                if (setting == null)
                {
                    AppendDescriptorValue(result, null);
                    continue;
                }

                AppendDescriptorValue(result, setting.Name);
                AppendDescriptorValue(result, setting.DisplayName);
                AppendDescriptorValue(result, setting.DataType);
                AppendDescriptorValue(
                    result,
                    setting.EnumType == null
                        ? null
                        : setting.EnumType.AssemblyQualifiedName);
                AppendDescriptorValue(
                    result,
                    setting.ObjectType == null
                        ? null
                        : setting.ObjectType.AssemblyQualifiedName);
                AppendDescriptorValue(result, setting.DefaultValue);
                AppendDescriptorValue(result, setting.IsRequired);
                AppendDescriptorValue(result, setting.Description);
                AppendDescriptorValue(result, setting.BindingMode);
                AppendDescriptorValue(result, setting.EvaluationPhase);
                AppendDescriptorValue(result, setting.AllowedVariableSources);
                AppendDescriptorValue(result, setting.AffectsDescriptor);
                AppendDescriptorValue(
                    result,
                    setting.VariableTypeValidator == null
                        ? null
                        : setting.VariableTypeValidator.Method.DeclaringType.FullName + "." + setting.VariableTypeValidator.Method.Name);
                if (setting.AffectsDescriptor)
                {
                    NodeSettingValue value;
                    if (node == null ||
                        node.Settings == null ||
                        !node.Settings.TryGetValue(setting.Name, out value) ||
                        value == null)
                    {
                        value = NodeSettingValue.ForConstant(setting.DefaultValue);
                    }

                    AppendDescriptorValue(result, value.Mode);
                    AppendDescriptorValue(result, value.ConstantValue);
                    AppendDescriptorValue(
                        result,
                        value.Selector == null
                            ? null
                            : VariableSelectionOption.FormatSelector(value.Selector));
                }
            }

            foreach (var output in descriptor.Outputs ?? new List<NodeOutputDescriptor>())
            {
                if (output == null)
                {
                    AppendDescriptorValue(result, null);
                    continue;
                }

                AppendDescriptorValue(result, output.Name);
                AppendDescriptorValue(result, output.DisplayName);
                AppendDescriptorValue(result, output.DataType);
                AppendDescriptorValue(
                    result,
                    output.EnumType == null
                        ? null
                        : output.EnumType.AssemblyQualifiedName);
                AppendDescriptorValue(result, output.Description);
            }

            return result.ToString();
        }

        private static void AppendPortSignature(
            StringBuilder result,
            string kind,
            NodePortDescriptor port)
        {
            AppendDescriptorValue(result, kind);
            if (port == null)
            {
                AppendDescriptorValue(result, null);
                return;
            }

            AppendDescriptorValue(result, port.Name);
            AppendDescriptorValue(result, port.DisplayName);
            AppendDescriptorValue(result, port.Direction);
            AppendDescriptorValue(result, port.DataType);
            AppendDescriptorValue(result, port.IsRequired);
            AppendDescriptorValue(result, port.Description);
        }

        private static void AppendDescriptorValue(StringBuilder result, object value)
        {
            var text = value == null
                ? string.Empty
                : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            result.Append(text.Length);
            result.Append(':');
            result.Append(text);
            result.Append('|');
        }

        private static NodeDefinition CloneNodeDefinition(NodeDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new NodeDefinition
            {
                Id = source.Id,
                Type = source.Type,
                Name = source.Name,
                Version = source.Version,
                ExecutionPolicy = CloneNodeExecutionPolicy(source.ExecutionPolicy)
            };
            clone.Settings.Clear();
            if (source.Settings != null)
            {
                foreach (var setting in source.Settings)
                {
                    clone.Settings[setting.Key] = CloneSettingValue(setting.Value);
                }
            }

            return clone;
        }

        private static void CopyEditableNodeState(NodeDefinition source, NodeDefinition target)
        {
            target.Name = source.Name;
            target.Settings = new Dictionary<string, NodeSettingValue>(
                StringComparer.OrdinalIgnoreCase);
            if (source.Settings != null)
            {
                foreach (var setting in source.Settings)
                {
                    target.Settings[setting.Key] = CloneSettingValue(setting.Value);
                }
            }

            target.ExecutionPolicy = CloneNodeExecutionPolicy(source.ExecutionPolicy);
        }

        private static string SerializeComparableNode(NodeDefinition node)
        {
            var document = new FlowDesignDocument
            {
                FlowId = "__property-draft__",
                FlowName = "__property-draft__"
            };
            document.Runtime.FlowId = document.FlowId;
            document.Runtime.FlowName = document.FlowName;
            document.Runtime.Version = "1.0.0";
            document.Runtime.Nodes.Add(CloneNodeDefinition(node));
            return FlowDesignSerializer.Serialize(document);
        }

        private PendingPropertyChangesDecision ShowPendingPropertyChangesDialog()
        {
            var decision = PendingPropertyChangesDecision.Cancel;
            var dialog = new FlowDesignerDialogWindow(
                "未应用的节点属性",
                464,
                264,
                Window.GetWindow(this));

            var root = new Grid { Margin = new Thickness(24, 18, 24, 18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(new TextBlock
            {
                Text = "节点属性尚未应用",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFromRgb(36, 50, 71)
            });
            var description = new TextBlock
            {
                Text = "继续操作前，请应用修改、放弃修改或取消当前操作。",
                Margin = new Thickness(0, 12, 0, 0),
                Foreground = BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(description, 1);
            root.Children.Add(description);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var apply = CreateDecisionButton("应用", FlowDesignerTheme.PrimaryButtonStyleKey);
            var discard = CreateDecisionButton("放弃", FlowDesignerTheme.SecondaryButtonStyleKey);
            var cancel = CreateDecisionButton("取消", FlowDesignerTheme.SecondaryButtonStyleKey);
            apply.Click += delegate
            {
                decision = PendingPropertyChangesDecision.Apply;
                dialog.DialogResult = true;
            };
            discard.Click += delegate
            {
                decision = PendingPropertyChangesDecision.Discard;
                dialog.DialogResult = true;
            };
            cancel.Click += delegate
            {
                decision = PendingPropertyChangesDecision.Cancel;
                dialog.DialogResult = false;
            };
            buttons.Children.Add(discard);
            buttons.Children.Add(cancel);
            buttons.Children.Add(apply);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);
            dialog.DialogContent = root;
            dialog.ShowDialog();
            return decision;
        }

        private static Button CreateDecisionButton(string text, string styleKey)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 82,
                Height = 36,
                Margin = new Thickness(8, 0, 0, 0)
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, styleKey);
            return button;
        }
    }
}
