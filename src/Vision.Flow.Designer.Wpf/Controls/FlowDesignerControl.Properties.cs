using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Designer.Wpf.Theming;

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
                _properties.SetPendingState(false, !CanEditDocument);
                return true;
            }

            if (!CanEditDocument)
            {
                error = "调试运行模式下不能应用节点属性。";
                _properties.ShowValidationError(error);
                return false;
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
            _properties.ResetEditorState();
            RenderCanvas();
            RenderProperties();
            _properties.ShowValidationError(null);
            AddDebugMessage("Applied properties for node " + _selectedNode.Id + ".");
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

            return _properties.HasEditorErrors ||
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
            _properties.ResetEditorState();
        }

        private void ClearPropertyDraft()
        {
            _propertyBaselineNode = null;
            _propertyDraftNode = null;
            _properties.ResetEditorState();
            _properties.SetPendingState(false, !CanEditDocument);
            _properties.ShowValidationError(null);
        }

        private void OnPropertyDraftChanged()
        {
            _properties.ShowValidationError(null);
            _properties.SetPendingState(HasPendingPropertyChangesCore(), !CanEditDocument);
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
