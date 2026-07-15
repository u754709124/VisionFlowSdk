using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

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

        /// <summary>
        /// 显示节点执行策略，并按只读状态启用或禁用全部编辑器。
        /// </summary>
        public void ShowPolicy(
            NodeDefinition node,
            NodeDescriptor descriptor,
            Action changed,
            bool isReadOnly)
        {
            Children.Clear();
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
                MinHeight = 28,
                Tag = TagPrefix + "FailureStrategy",
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
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
            if (_policy.FailureStrategy == FailureStrategy.DefaultOutputs && !_isReadOnly)
            {
                EnsureDefaultOutputs();
            }

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
                editor.Checked += delegate { SetDefaultOutput(output.Name, true); };
                editor.Unchecked += delegate { SetDefaultOutput(output.Name, false); };
                return editor;
            }

            var editorText = ToEditorText(output.DataType, value);
            var textBox = new TextBox
            {
                Text = editorText,
                IsReadOnly = _isReadOnly,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                Tag = tag,
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            textBox.LostFocus += delegate
            {
                if (_isReadOnly)
                {
                    return;
                }

                object converted;
                if (!TryConvertEditorText(output.DataType, textBox.Text, out converted))
                {
                    textBox.Text = editorText;
                    textBox.ToolTip = "输入值不能转换为 " + FlowEnumConverter.ToWireValue(output.DataType) + "。";
                    return;
                }

                editorText = ToEditorText(output.DataType, converted);
                textBox.Text = editorText;
                textBox.ToolTip = null;
                SetDefaultOutput(output.Name, converted);
            };
            return textBox;
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
            var originalValue = value;
            var textBox = new TextBox
            {
                Text = value.ToString(CultureInfo.InvariantCulture),
                IsReadOnly = _isReadOnly,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                Tag = tag,
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            textBox.LostFocus += delegate
            {
                if (_isReadOnly)
                {
                    return;
                }

                int parsed;
                if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < minimum)
                {
                    textBox.Text = originalValue.ToString(CultureInfo.InvariantCulture);
                    textBox.ToolTip = "请输入不小于 " + minimum.ToString(CultureInfo.InvariantCulture) + " 的整数。";
                    return;
                }

                originalValue = parsed;
                textBox.Text = parsed.ToString(CultureInfo.InvariantCulture);
                textBox.ToolTip = null;
                setter(parsed);
                RaiseChanged();
            };
            layout.Children.Add(textBox);
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
