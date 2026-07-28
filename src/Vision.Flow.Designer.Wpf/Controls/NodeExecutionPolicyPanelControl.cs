using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Designer.Wpf.Theming;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 编辑节点通用执行策略。执行策略属于静态控制面，不提供变量选择器。
    /// </summary>
    public sealed class NodeExecutionPolicyPanelControl : StackPanel
    {
        private const string TagPrefix = "ExecutionPolicy.";
        private NodeDescriptor _descriptor;
        private NodeExecutionPolicy _policy;
        private RetryPolicy _retryPolicy;
        private Action _changed;
        private bool _isReadOnly;
        private ContentControl _retryDetailsHost;
        private ContentControl _failureDetailsHost;
        private readonly Dictionary<string, string> _validationErrors =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _rawEditorTexts =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Control> _fieldEditors =
            new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        private string _renderedNodeId;

        public bool HasValidationErrors
        {
            get { return _validationErrors.Count > 0; }
        }

        /// <summary>
        /// 显示节点执行策略，并按只读状态启用或禁用全部编辑器。
        /// </summary>
        public void ShowPolicy(
            NodeDefinition node,
            NodeDescriptor descriptor,
            Action changed,
            bool isReadOnly)
        {
            var nodeId = node == null ? null : node.Id;
            if (!string.Equals(_renderedNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            {
                ResetEditorState();
                _renderedNodeId = nodeId;
            }

            Children.Clear();
            _fieldEditors.Clear();
            _descriptor = descriptor;
            _changed = changed;
            _isReadOnly = isReadOnly;

            if (node == null)
            {
                Children.Add(CreateMutedText("请选择节点。"));
                return;
            }

            _policy = node.ExecutionPolicy ?? new NodeExecutionPolicy();
            if (node.ExecutionPolicy == null && !_isReadOnly)
            {
                node.ExecutionPolicy = _policy;
            }

            _retryPolicy = _policy.RetryPolicy ?? new RetryPolicy();
            if (_policy.RetryPolicy == null && !_isReadOnly)
            {
                _policy.RetryPolicy = _retryPolicy;
            }

            if (_policy.DefaultOutputs == null && !_isReadOnly)
            {
                _policy.DefaultOutputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            AddIntegerField(
                "单次超时（毫秒）",
                "0 表示继承流程全局超时设置。",
                TagPrefix + "TimeoutMs",
                _policy.TimeoutMs,
                0,
                delegate(int value) { _policy.TimeoutMs = value; });
            AddIntegerField(
                "最大并发执行数",
                "限制同一节点实例可以同时执行的次数。",
                TagPrefix + "MaxConcurrentExecutions",
                _policy.MaxConcurrentExecutions,
                1,
                delegate(int value) { _policy.MaxConcurrentExecutions = value; });

            Children.Add(CreateLabel("重试"));
            var retryToggle = new CheckBox
            {
                Content = "启用重试",
                IsChecked = _retryPolicy.Enabled,
                IsEnabled = !_isReadOnly,
                Tag = TagPrefix + "RetryPolicy.Enabled",
                Margin = new Thickness(0, 2, 0, 4)
            };
            retryToggle.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.SwitchCheckBoxStyleKey);
            retryToggle.Checked += delegate
            {
                if (_isReadOnly)
                {
                    return;
                }

                _retryPolicy.Enabled = true;
                RaiseChanged();
                RenderRetryDetails();
            };
            retryToggle.Unchecked += delegate
            {
                if (_isReadOnly)
                {
                    return;
                }

                _retryPolicy.Enabled = false;
                RaiseChanged();
                RenderRetryDetails();
            };
            Children.Add(retryToggle);

            _retryDetailsHost = new ContentControl();
            Children.Add(_retryDetailsHost);
            RenderRetryDetails();

            Children.Add(CreateLabel("失败处理"));
            var failureSelector = new ComboBox
            {
                IsEnabled = !_isReadOnly,
                Tag = TagPrefix + "FailureStrategy"
            };
            failureSelector.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.FieldComboBoxStyleKey);
            AddFailureStrategyItem(failureSelector, "停止流程", FailureStrategy.StopFlow);
            AddFailureStrategyItem(failureSelector, "转入异常分支", FailureStrategy.ErrorBranch);
            AddFailureStrategyItem(failureSelector, "使用默认输出", FailureStrategy.DefaultOutputs);
            failureSelector.SelectedItem = failureSelector.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(x => Equals(x.Tag, _policy.FailureStrategy));
            failureSelector.SelectionChanged += delegate
            {
                if (_isReadOnly || failureSelector.SelectedItem == null)
                {
                    return;
                }

                var strategy = (FailureStrategy)((ComboBoxItem)failureSelector.SelectedItem).Tag;
                if (_policy.FailureStrategy == strategy)
                {
                    return;
                }

                _policy.FailureStrategy = strategy;
                if (strategy == FailureStrategy.DefaultOutputs)
                {
                    EnsureDefaultOutputs();
                }

                RaiseChanged();
                RenderFailureDetails();
            };
            Children.Add(failureSelector);

            _failureDetailsHost = new ContentControl();
            Children.Add(_failureDetailsHost);
            RenderFailureDetails();
        }

        private void RenderRetryDetails()
        {
            if (_retryDetailsHost == null)
            {
                return;
            }

            if (!_retryPolicy.Enabled)
            {
                RemoveEditorState(TagPrefix + "RetryPolicy.");
                _retryDetailsHost.Content = CreateMutedText("关闭后节点只执行一次。重试参数会保留，但运行时不会使用。");
                return;
            }

            var layout = new StackPanel();
            AddIntegerField(
                layout,
                "最大重试次数",
                "不包含首次执行。",
                TagPrefix + "RetryPolicy.MaxRetries",
                _retryPolicy.MaxRetries,
                0,
                delegate(int value) { _retryPolicy.MaxRetries = value; });
            AddIntegerField(
                layout,
                "重试间隔（毫秒）",
                "每次重试前使用固定等待时间。",
                TagPrefix + "RetryPolicy.RetryIntervalMs",
                _retryPolicy.RetryIntervalMs,
                0,
                delegate(int value) { _retryPolicy.RetryIntervalMs = value; });
            _retryDetailsHost.Content = layout;
        }

        private void RenderFailureDetails()
        {
            if (_failureDetailsHost == null)
            {
                return;
            }

            var layout = new StackPanel();
            if (_policy.FailureStrategy != FailureStrategy.DefaultOutputs)
            {
                RemoveEditorState(TagPrefix + "DefaultOutputs.");
            }
            switch (_policy.FailureStrategy)
            {
                case FailureStrategy.ErrorBranch:
                    layout.Children.Add(CreateMutedText("节点最终失败后沿 Error 或 Timeout 控制端口继续；没有对应连线时本次流程失败。"));
                    break;
                case FailureStrategy.DefaultOutputs:
                    layout.Children.Add(CreateMutedText("节点最终失败后写入以下常量回退输出，并沿 Next 控制端口继续。"));
                    AddDefaultOutputEditors(layout);
                    break;
                default:
                    layout.Children.Add(CreateMutedText("节点最终失败后停止本次流程运行。"));
                    break;
            }

            _failureDetailsHost.Content = layout;
        }

        private void AddDefaultOutputEditors(Panel layout)
        {
            var outputs = _descriptor == null || _descriptor.Outputs == null
                ? new List<NodeOutputDescriptor>()
                : _descriptor.Outputs.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)).ToList();
            if (outputs.Count == 0)
            {
                layout.Children.Add(CreateMutedText("此节点没有声明输出变量，无需配置回退值。"));
                return;
            }

            foreach (var output in outputs)
            {
                layout.Children.Add(CreateLabel(
                    (string.IsNullOrWhiteSpace(output.DisplayName) ? output.Name : output.DisplayName) +
                    " (" + output.Name + ") · " + FlowEnumConverter.ToWireValue(output.DataType)));

                object value = null;
                var hasValue = _policy.DefaultOutputs != null && _policy.DefaultOutputs.TryGetValue(output.Name, out value);
                object defaultValue;
                if (!TryCreateDefaultValue(output.DataType, out defaultValue))
                {
                    layout.Children.Add(CreateInvalidText("该输出类型不能由属性面板创建常量回退值。请选择其他失败策略。"));
                    continue;
                }

                if (!hasValue)
                {
                    value = defaultValue;
                }

                layout.Children.Add(CreateDefaultOutputEditor(output, value));
            }
        }

        private UIElement CreateDefaultOutputEditor(NodeOutputDescriptor output, object value)
        {
            var tag = TagPrefix + "DefaultOutputs." + output.Name;
            if (output.DataType == FlowDataType.Boolean)
            {
                var editor = new CheckBox
                {
                    Content = "启用",
                    IsChecked = value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                    IsEnabled = !_isReadOnly,
                    Tag = tag,
                    Margin = new Thickness(0, 1, 0, 4)
                };
                editor.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.SwitchCheckBoxStyleKey);
                editor.Checked += delegate { SetDefaultOutput(output.Name, true); };
                editor.Unchecked += delegate { SetDefaultOutput(output.Name, false); };
                return editor;
            }

            var editorText = GetRawEditorText(tag, ToEditorText(output.DataType, value));
            var textBox = new TextBox
            {
                Text = editorText,
                IsReadOnly = _isReadOnly,
                Tag = tag
            };
            textBox.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.FieldTextBoxStyleKey);
            var errorText = CreateInlineError();
            _fieldEditors[tag] = textBox;
            RestoreFieldError(tag, textBox, errorText);
            var isNormalizing = false;
            Action applyText = delegate
            {
                if (_isReadOnly || isNormalizing)
                {
                    return;
                }

                _rawEditorTexts[tag] = textBox.Text ?? string.Empty;
                object converted;
                if (!TryConvertEditorText(output.DataType, textBox.Text, out converted))
                {
                    SetFieldError(
                        tag,
                        "输入值不能转换为 " + FlowEnumConverter.ToWireValue(output.DataType) + "。",
                        textBox,
                        errorText);
                    RaiseChanged();
                    return;
                }

                ClearFieldError(tag, textBox, errorText);
                SetDefaultOutput(output.Name, converted);
            };
            textBox.TextChanged += delegate { applyText(); };
            textBox.LostFocus += delegate
            {
                if (_isReadOnly || _validationErrors.ContainsKey(tag))
                {
                    return;
                }

                object converted;
                if (!TryConvertEditorText(output.DataType, textBox.Text, out converted))
                {
                    return;
                }

                editorText = ToEditorText(output.DataType, converted);
                isNormalizing = true;
                textBox.Text = editorText;
                isNormalizing = false;
                _rawEditorTexts[tag] = editorText;
            };
            var layout = new StackPanel();
            layout.Children.Add(textBox);
            layout.Children.Add(errorText);
            return layout;
        }

        private void AddIntegerField(
            string label,
            string help,
            string tag,
            int value,
            int minimum,
            Action<int> setter)
        {
            AddIntegerField(this, label, help, tag, value, minimum, setter);
        }

        private void AddIntegerField(
            Panel layout,
            string label,
            string help,
            string tag,
            int value,
            int minimum,
            Action<int> setter)
        {
            layout.Children.Add(CreateLabel(label));
            var textBox = new TextBox
            {
                Text = GetRawEditorText(tag, value.ToString(CultureInfo.InvariantCulture)),
                IsReadOnly = _isReadOnly,
                Tag = tag
            };
            textBox.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.FieldTextBoxStyleKey);
            var errorText = CreateInlineError();
            _fieldEditors[tag] = textBox;
            RestoreFieldError(tag, textBox, errorText);
            var isNormalizing = false;
            Action applyText = delegate
            {
                if (_isReadOnly || isNormalizing)
                {
                    return;
                }

                _rawEditorTexts[tag] = textBox.Text ?? string.Empty;
                int parsed;
                if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < minimum)
                {
                    SetFieldError(
                        tag,
                        "请输入不小于 " + minimum.ToString(CultureInfo.InvariantCulture) + " 的整数。",
                        textBox,
                        errorText);
                    RaiseChanged();
                    return;
                }

                ClearFieldError(tag, textBox, errorText);
                setter(parsed);
                RaiseChanged();
            };
            textBox.TextChanged += delegate { applyText(); };
            textBox.LostFocus += delegate
            {
                if (_isReadOnly || _validationErrors.ContainsKey(tag))
                {
                    return;
                }

                int parsed;
                if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    return;
                }

                var normalized = parsed.ToString(CultureInfo.InvariantCulture);
                isNormalizing = true;
                textBox.Text = normalized;
                isNormalizing = false;
                _rawEditorTexts[tag] = normalized;
            };
            layout.Children.Add(textBox);
            layout.Children.Add(errorText);
            layout.Children.Add(CreateMutedText(help));
        }

        private void EnsureDefaultOutputs()
        {
            if (_isReadOnly || _descriptor == null || _descriptor.Outputs == null)
            {
                return;
            }

            if (_policy.DefaultOutputs == null)
            {
                _policy.DefaultOutputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var output in _descriptor.Outputs.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                if (_policy.DefaultOutputs.ContainsKey(output.Name))
                {
                    continue;
                }

                object value;
                if (TryCreateDefaultValue(output.DataType, out value))
                {
                    _policy.DefaultOutputs[output.Name] = value;
                }
            }
        }

        private void SetDefaultOutput(string name, object value)
        {
            if (_isReadOnly)
            {
                return;
            }

            if (_policy.DefaultOutputs == null)
            {
                _policy.DefaultOutputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            _policy.DefaultOutputs[name] = value;
            RaiseChanged();
        }

        private static void AddFailureStrategyItem(ItemsControl selector, string displayName, FailureStrategy strategy)
        {
            selector.Items.Add(new ComboBoxItem { Content = displayName, Tag = strategy });
        }

        private static bool TryCreateDefaultValue(FlowDataType dataType, out object value)
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

        private static string ToEditorText(FlowDataType dataType, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (dataType == FlowDataType.DateTime)
            {
                return Convert.ToDateTime(value, CultureInfo.InvariantCulture).ToString("o", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static bool TryConvertEditorText(FlowDataType dataType, string text, out object value)
        {
            switch (dataType)
            {
                case FlowDataType.String:
                case FlowDataType.Object:
                    value = text ?? string.Empty;
                    return true;
                case FlowDataType.Int32:
                    int intValue;
                    value = intValue = 0;
                    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue) && Assign(out value, intValue);
                case FlowDataType.Int64:
                    long longValue;
                    value = longValue = 0L;
                    return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue) && Assign(out value, longValue);
                case FlowDataType.Double:
                    double doubleValue;
                    value = doubleValue = 0.0d;
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue) && Assign(out value, doubleValue);
                case FlowDataType.DateTime:
                    DateTime dateTimeValue;
                    value = dateTimeValue = DateTime.MinValue;
                    return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dateTimeValue) && Assign(out value, dateTimeValue);
                default:
                    value = null;
                    return false;
            }
        }

        private static bool Assign<T>(out object target, T value)
        {
            target = value;
            return true;
        }

        private void RaiseChanged()
        {
            if (_changed != null)
            {
                _changed();
            }
        }

        public bool TryValidate(out string error)
        {
            error = _validationErrors.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (!string.IsNullOrWhiteSpace(error))
            {
                FocusFirstValidationError();
                return false;
            }

            if (_policy == null)
            {
                return true;
            }

            if (_policy.TimeoutMs < 0)
            {
                error = "单次超时不能小于 0。";
                SetModelValidationError(TagPrefix + "TimeoutMs", error);
                return false;
            }

            if (_policy.MaxConcurrentExecutions < 1)
            {
                error = "最大并发执行数不能小于 1。";
                SetModelValidationError(TagPrefix + "MaxConcurrentExecutions", error);
                return false;
            }

            if (_retryPolicy != null && _retryPolicy.Enabled)
            {
                if (_retryPolicy.MaxRetries < 0)
                {
                    error = "最大重试次数不能小于 0。";
                    SetModelValidationError(TagPrefix + "RetryPolicy.MaxRetries", error);
                    return false;
                }

                if (_retryPolicy.RetryIntervalMs < 0)
                {
                    error = "重试间隔不能小于 0。";
                    SetModelValidationError(TagPrefix + "RetryPolicy.RetryIntervalMs", error);
                    return false;
                }
            }

            return true;
        }

        public void ResetEditorState()
        {
            _validationErrors.Clear();
            _rawEditorTexts.Clear();
            _fieldEditors.Clear();
            _renderedNodeId = null;
        }

        private static TextBlock CreateInlineError()
        {
            var error = new TextBlock
            {
                Foreground = FlowDesignerControl.BrushFromRgb(209, 67, 67),
                FontSize = 11,
                Margin = new Thickness(1, 3, 0, 2),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            error.SetResourceReference(
                FrameworkElement.StyleProperty,
                FlowDesignerTheme.ErrorTextStyleKey);
            return error;
        }

        private void SetFieldError(string tag, string error, Control editor, TextBlock errorText)
        {
            _validationErrors[tag] = error;
            editor.BorderBrush = FlowDesignerControl.BrushFromRgb(209, 67, 67);
            editor.ToolTip = error;
            errorText.Text = error;
            errorText.Visibility = Visibility.Visible;
        }

        private void ClearFieldError(string tag, Control editor, TextBlock errorText)
        {
            _validationErrors.Remove(tag);
            editor.ClearValue(Control.BorderBrushProperty);
            editor.ToolTip = null;
            errorText.Text = string.Empty;
            errorText.Visibility = Visibility.Collapsed;
        }

        private void RemoveValidationErrors(string prefix)
        {
            foreach (var key in _validationErrors.Keys
                .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                _validationErrors.Remove(key);
            }
        }

        private void RemoveEditorState(string prefix)
        {
            RemoveValidationErrors(prefix);
            foreach (var key in _rawEditorTexts.Keys
                .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                _rawEditorTexts.Remove(key);
            }
        }

        private string GetRawEditorText(string tag, string fallback)
        {
            string raw;
            return _rawEditorTexts.TryGetValue(tag, out raw) ? raw : fallback;
        }

        private void RestoreFieldError(string tag, Control editor, TextBlock errorText)
        {
            string error;
            if (_validationErrors.TryGetValue(tag, out error) && !string.IsNullOrWhiteSpace(error))
            {
                editor.BorderBrush = FlowDesignerControl.BrushFromRgb(209, 67, 67);
                editor.ToolTip = error;
                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
            }
        }

        private void FocusFirstValidationError()
        {
            foreach (var tag in _validationErrors.Keys)
            {
                Control editor;
                if (_fieldEditors.TryGetValue(tag, out editor))
                {
                    editor.BringIntoView();
                    editor.Focus();
                    return;
                }
            }
        }

        private void SetModelValidationError(string tag, string error)
        {
            _validationErrors[tag] = error;
            Control editor;
            if (_fieldEditors.TryGetValue(tag, out editor))
            {
                editor.BorderBrush = FlowDesignerControl.BrushFromRgb(209, 67, 67);
                editor.ToolTip = error;
                editor.BringIntoView();
                editor.Focus();
            }
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 8, 0, 3),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock CreateMutedText(string text)
        {
            return CreateStatusText(text, FlowDesignerControl.BrushFromRgb(100, 116, 139));
        }

        private static TextBlock CreateInvalidText(string text)
        {
            return CreateStatusText(text, FlowDesignerControl.BrushFromRgb(185, 28, 28));
        }

        private static TextBlock CreateStatusText(string text, Brush foreground)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = foreground,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 3)
            };
        }
    }
}
