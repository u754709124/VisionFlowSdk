using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Designer.Wpf.ViewModels;
using Vision.Flow.Nodes;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 属性面板按 Descriptor 编辑节点配置；可绑定配置在固定值和结构化变量之间切换。
    /// </summary>
    public sealed class PropertyPanelControl : Border
    {
        private readonly StackPanel _rows;
        private Action _changed;
        private IList<VariableSelectionOption> _variableOptions;
        private bool _isReadOnly;

        public PropertyPanelControl()
        {
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            _rows = new StackPanel();
            _variableOptions = new List<VariableSelectionOption>();
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _rows
            };
        }

        public void ShowNode(NodeDefinition node, NodeDescriptor descriptor, Action changed)
        {
            ShowNode(node, descriptor, null, null, changed, false);
        }

        public void ShowNode(
            NodeDefinition node,
            NodeDescriptor descriptor,
            IEnumerable<VariableSelectionOption> variableOptions,
            Action changed)
        {
            ShowNode(node, descriptor, variableOptions, null, changed, false);
        }

        public void ShowNode(
            NodeDefinition node,
            NodeDescriptor descriptor,
            IEnumerable<VariableSelectionOption> variableOptions,
            Action changed,
            bool isReadOnly)
        {
            ShowNode(node, descriptor, variableOptions, null, changed, isReadOnly);
        }

        public void ShowNode(
            NodeDefinition node,
            NodeDescriptor descriptor,
            IEnumerable<VariableSelectionOption> variableOptions,
            IEnumerable<string> variableIssues,
            Action changed,
            bool isReadOnly)
        {
            _changed = changed;
            _isReadOnly = isReadOnly;
            _variableOptions = variableOptions == null
                ? new List<VariableSelectionOption>()
                : variableOptions.Where(x => x != null && x.Selector != null).ToList();
            _rows.Children.Clear();

            _rows.Children.Add(CreateTitle("Properties"));
            if (node == null)
            {
                _rows.Children.Add(CreateMutedText("Select a node on the canvas."));
                return;
            }

            AddTextField("Id", node.Id, false, null);
            AddTextField("Name", node.Name, true, delegate(string text) { node.Name = text; });
            AddTextField("Type", node.Type, false, null);

            _rows.Children.Add(CreateSection("Settings"));
            if (variableIssues != null)
            {
                foreach (var issue in variableIssues.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    _rows.Children.Add(CreateInvalidText(issue));
                }
            }

            if (descriptor != null)
            {
                foreach (var setting in descriptor.Settings)
                {
                    NodeSettingValue value;
                    node.Settings.TryGetValue(setting.Name, out value);
                    if (value == null)
                    {
                        value = NodeSettingValue.ForConstant(setting.DefaultValue);
                        if (!_isReadOnly)
                        {
                            node.Settings[setting.Name] = value;
                        }
                    }

                    AddSettingField(setting, value, delegate(NodeSettingValue newValue)
                    {
                        node.Settings[setting.Name] = newValue;
                    });
                }
            }

            _rows.Children.Add(CreateSection("执行策略"));
            var executionPolicyPanel = new NodeExecutionPolicyPanelControl();
            executionPolicyPanel.ShowPolicy(node, descriptor, RaiseChanged, _isReadOnly);
            _rows.Children.Add(executionPolicyPanel);

            if (descriptor != null && descriptor.Outputs.Count > 0)
            {
                _rows.Children.Add(CreateSection("Outputs"));
                foreach (var output in descriptor.Outputs)
                {
                    _rows.Children.Add(CreateMutedText(output.Name + " : " + FlowEnumConverter.ToWireValue(output.DataType)));
                }
            }
        }

        private void AddSettingField(NodeSettingDescriptor setting, NodeSettingValue value, Action<NodeSettingValue> setter)
        {
            _rows.Children.Add(CreateLabel(setting.DisplayName + " (" + setting.Name + ")"));
            var current = value ?? NodeSettingValue.ForConstant(setting.DefaultValue);
            if (setting.BindingMode != NodeSettingBindingMode.ConstantOrVariable ||
                setting.EvaluationPhase != NodeSettingEvaluationPhase.Execution)
            {
                if (current.Mode == NodeSettingValueMode.Variable)
                {
                    _rows.Children.Add(CreateInvalidText(setting.EvaluationPhase == NodeSettingEvaluationPhase.ListenerStart
                        ? "该配置项在监听启动阶段求值，不能使用执行期变量；当前选择会保留并由校验器报告：" + VariableSelectionOption.FormatSelector(current.Selector)
                        : "该配置项只允许固定值；当前变量选择会保留并由校验器报告：" + VariableSelectionOption.FormatSelector(current.Selector)));
                }

                _rows.Children.Add(CreateConstantEditor(setting, current.ConstantValue, delegate(object constantValue)
                {
                    ApplySetting(setter, NodeSettingValue.ForConstant(constantValue));
                }));
                return;
            }

            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            var modeSelector = new ComboBox
            {
                IsEnabled = !_isReadOnly,
                MinHeight = 28,
                Tag = setting.Name + ":Mode",
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            modeSelector.Items.Add(new ComboBoxItem { Content = "固定值", Tag = NodeSettingValueMode.Constant });
            modeSelector.Items.Add(new ComboBoxItem { Content = "变量", Tag = NodeSettingValueMode.Variable });
            modeSelector.SelectedIndex = current.Mode == NodeSettingValueMode.Variable ? 1 : 0;
            container.Children.Add(modeSelector);

            var editorHost = new ContentControl { Margin = new Thickness(0, 4, 0, 0) };
            container.Children.Add(editorHost);

            Action renderEditor = null;
            renderEditor = delegate
            {
                if (current.Mode == NodeSettingValueMode.Variable)
                {
                    editorHost.Content = CreateVariableEditor(setting, current, delegate(NodeSettingValue newValue)
                    {
                        current = newValue;
                        ApplySetting(setter, current);
                    });
                }
                else
                {
                    editorHost.Content = CreateConstantEditor(setting, current.ConstantValue, delegate(object constantValue)
                    {
                        current = NodeSettingValue.ForConstant(constantValue);
                        ApplySetting(setter, current);
                    });
                }
            };

            modeSelector.SelectionChanged += delegate
            {
                if (_isReadOnly || modeSelector.SelectedItem == null)
                {
                    return;
                }

                var mode = (NodeSettingValueMode)((ComboBoxItem)modeSelector.SelectedItem).Tag;
                if (mode == current.Mode)
                {
                    return;
                }

                current = mode == NodeSettingValueMode.Constant
                    ? NodeSettingValue.ForConstant(current.ConstantValue)
                    : new NodeSettingValue
                    {
                        Mode = NodeSettingValueMode.Variable,
                        ConstantValue = current.ConstantValue,
                        Selector = current.Selector
                    };
                setter(current);
                RaiseChanged();
                renderEditor();
            };

            renderEditor();
            _rows.Children.Add(container);
        }

        private UIElement CreateVariableEditor(NodeSettingDescriptor setting, NodeSettingValue value, Action<NodeSettingValue> setter)
        {
            var allowedOptions = _variableOptions
                .Where(x => IsSourceAllowed(setting.AllowedVariableSources, x.Selector.Scope))
                .ToList();
            var compatibleOptions = allowedOptions
                .Where(x => FlowDataTypeCompatibility.IsCompatible(x.DataType, setting.DataType))
                .ToList();

            var layout = new StackPanel();
            var selector = new VariableSelectorControl(compatibleOptions)
            {
                IsEnabled = !_isReadOnly
            };
            selector.ShowSelector(value.Selector);
            layout.Children.Add(selector);

            var statusHost = new ContentControl
            {
                Content = CreateVariableStatus(setting, value.Selector, allowedOptions, compatibleOptions)
            };
            layout.Children.Add(statusHost);

            selector.VariableSelected += delegate(VariableSelectionOption selected)
            {
                if (!_isReadOnly && selected != null)
                {
                    value = NodeSettingValue.ForVariable(CloneSelector(selected.Selector), value.ConstantValue);
                    setter(value);
                    statusHost.Content = CreateVariableStatus(
                        setting,
                        value.Selector,
                        allowedOptions,
                        compatibleOptions);
                }
            };
            return layout;
        }

        private UIElement CreateConstantEditor(NodeSettingDescriptor setting, object value, Action<object> setter)
        {
            if (setting.DataType == FlowDataType.Boolean)
            {
                var checkBox = new CheckBox
                {
                    IsChecked = value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                    IsEnabled = !_isReadOnly,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                checkBox.Checked += delegate { if (!_isReadOnly) setter(true); };
                checkBox.Unchecked += delegate { if (!_isReadOnly) setter(false); };
                return checkBox;
            }

            var selectorItems = GetSelectorItems(setting);
            if (selectorItems.Count > 0)
            {
                var comboBox = new ComboBox
                {
                    IsEditable = true,
                    IsEnabled = !_isReadOnly,
                    Text = ToEditorText(setting, value),
                    MinHeight = 28,
                    Margin = new Thickness(0, 0, 0, 4),
                    BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
                };
                foreach (var item in selectorItems)
                {
                    comboBox.Items.Add(item);
                }

                comboBox.LostFocus += delegate
                {
                    if (!_isReadOnly) setter(ConvertFromEditorText(setting, comboBox.Text));
                };
                comboBox.DropDownClosed += delegate
                {
                    if (!_isReadOnly) setter(ConvertFromEditorText(setting, comboBox.Text));
                };
                return comboBox;
            }

            var textBox = new TextBox
            {
                Text = ToEditorText(setting, value),
                IsReadOnly = _isReadOnly,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = setting.Name.IndexOf("Mappings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    setting.Name.IndexOf("Channels", StringComparison.OrdinalIgnoreCase) >= 0,
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            textBox.LostFocus += delegate
            {
                if (!_isReadOnly) setter(ConvertFromEditorText(setting, textBox.Text));
            };
            return textBox;
        }

        private static UIElement CreateVariableStatus(
            NodeSettingDescriptor setting,
            VariableSelector selector,
            IList<VariableSelectionOption> allowedOptions,
            IList<VariableSelectionOption> compatibleOptions)
        {
            if (selector == null || selector.Path == null || selector.Path.Count == 0)
            {
                return CreateInvalidText("请选择变量。固定值会被保留，但变量模式不会使用它。");
            }

            if (!IsSourceAllowed(setting.AllowedVariableSources, selector.Scope))
            {
                return CreateInvalidText("当前配置项不允许该变量范围：" + FlowEnumConverter.ToWireValue(selector.Scope));
            }

            var source = allowedOptions.FirstOrDefault(x => x.Matches(selector));
            if (source == null)
            {
                return CreateInvalidText("变量来源不可用：" + VariableSelectionOption.FormatSelector(selector));
            }

            if (!compatibleOptions.Any(x => x.Matches(selector)))
            {
                return CreateInvalidText("变量类型 " + FlowEnumConverter.ToWireValue(source.DataType) +
                    " 不能赋给 " + FlowEnumConverter.ToWireValue(setting.DataType) + "。");
            }

            if (FlowDataTypeCompatibility.GetCompatibility(source.DataType, setting.DataType) == FlowDataTypeCompatibilityResult.Warning)
            {
                return CreateWarningText("变量类型需要在运行时转换为 " + FlowEnumConverter.ToWireValue(setting.DataType) + "。");
            }

            return null;
        }

        private static bool IsSourceAllowed(VariableSelectorScopeFlags flags, VariableSelectorScope scope)
        {
            switch (scope)
            {
                case VariableSelectorScope.NodeOutput:
                    return (flags & VariableSelectorScopeFlags.NodeOutput) != 0;
                case VariableSelectorScope.TriggerInput:
                    return (flags & VariableSelectorScopeFlags.TriggerInput) != 0;
                case VariableSelectorScope.Token:
                    return (flags & VariableSelectorScopeFlags.Token) != 0;
                default:
                    return false;
            }
        }

        private static VariableSelector CloneSelector(VariableSelector selector)
        {
            return selector == null
                ? null
                : new VariableSelector
                {
                    Scope = selector.Scope,
                    Path = selector.Path == null ? new List<string>() : new List<string>(selector.Path)
                };
        }

        private void AddTextField(string label, string value, bool editable, Action<string> setter)
        {
            _rows.Children.Add(CreateLabel(label));
            var textBox = new TextBox
            {
                Text = value ?? string.Empty,
                IsReadOnly = !editable || _isReadOnly,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                TextWrapping = TextWrapping.Wrap,
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            textBox.LostFocus += delegate
            {
                if (!_isReadOnly && setter != null)
                {
                    setter(textBox.Text);
                    RaiseChanged();
                }
            };
            _rows.Children.Add(textBox);
        }

        private void ApplySetting(Action<NodeSettingValue> setter, NodeSettingValue value)
        {
            if (!_isReadOnly && setter != null)
            {
                setter(value);
                RaiseChanged();
            }
        }

        private void RaiseChanged()
        {
            if (_changed != null)
            {
                _changed();
            }
        }

        private static TextBlock CreateLabel(string label)
        {
            return new TextBlock
            {
                Text = label,
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 8, 0, 3)
            };
        }

        private static TextBlock CreateTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private static TextBlock CreateSection(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(15, 23, 42),
                Margin = new Thickness(0, 14, 0, 4)
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

        private static TextBlock CreateWarningText(string text)
        {
            return CreateStatusText(text, FlowDesignerControl.BrushFromRgb(180, 83, 9));
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

        private static IList<string> GetSelectorItems(NodeSettingDescriptor setting)
        {
            var items = new List<string>();
            if (setting == null)
            {
                return items;
            }

            if (string.Equals(setting.Name, "CameraId", StringComparison.OrdinalIgnoreCase))
            {
                items.Add("Camera01");
            }
            else if (string.Equals(setting.Name, FlowSettingNames.DuplicatePolicy, StringComparison.OrdinalIgnoreCase))
            {
                AddWireValues<FlowDuplicatePolicy>(items);
            }
            else if (string.Equals(setting.Name, FlowSettingNames.Operator, StringComparison.OrdinalIgnoreCase))
            {
                AddWireValues<ConditionOperator>(items);
            }
            else if (string.Equals(setting.Name, FlowSettingNames.Level, StringComparison.OrdinalIgnoreCase))
            {
                AddWireValues<FlowLogLevel>(items);
            }

            return items;
        }

        private static void AddWireValues<TEnum>(IList<string> items)
            where TEnum : struct
        {
            var values = FlowEnumConverter.GetWireValues<TEnum>();
            for (var index = 0; index < values.Length; index++)
            {
                items.Add(values[index]);
            }
        }

        private static string ToEditorText(NodeSettingDescriptor setting, object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (setting != null && string.Equals(setting.Name, "Channels", StringComparison.OrdinalIgnoreCase))
            {
                return ToPairText(value, "ChannelName", "Intensity");
            }

            if (setting != null && string.Equals(setting.Name, "Parameters", StringComparison.OrdinalIgnoreCase))
            {
                return ToPairText(value, "Name", "Value");
            }

            if (setting != null && string.Equals(setting.Name, "FieldMappings", StringComparison.OrdinalIgnoreCase))
            {
                return ToPairText(value, "FieldName", "Value");
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object ConvertFromEditorText(NodeSettingDescriptor setting, string text)
        {
            if (setting == null)
            {
                return text;
            }

            if (string.Equals(setting.Name, "Channels", StringComparison.OrdinalIgnoreCase))
            {
                return ParseChannels(text);
            }

            if (string.Equals(setting.Name, "Parameters", StringComparison.OrdinalIgnoreCase))
            {
                return ParseParameters(text);
            }

            if (string.Equals(setting.Name, "FieldMappings", StringComparison.OrdinalIgnoreCase))
            {
                return ParseFieldMappings(text);
            }

            if (setting.DataType == FlowDataType.Int32)
            {
                int intValue;
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue) ? intValue : 0;
            }

            if (setting.DataType == FlowDataType.Int64)
            {
                long longValue;
                return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue) ? longValue : 0L;
            }

            if (setting.DataType == FlowDataType.Double)
            {
                double doubleValue;
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue) ? doubleValue : 0.0;
            }

            if (setting.DataType == FlowDataType.Boolean)
            {
                bool boolValue;
                return bool.TryParse(text, out boolValue) && boolValue;
            }

            return string.IsNullOrWhiteSpace(text) && !setting.IsRequired ? null : text;
        }

        private static List<Dictionary<string, object>> ParseChannels(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var pair in ParsePairs(text))
            {
                double intensity;
                double.TryParse(pair.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out intensity);
                result.Add(new Dictionary<string, object>
                {
                    { "ChannelName", pair.Key },
                    { "IsEnabled", true },
                    { "Intensity", intensity },
                    { "DurationMs", 0 }
                });
            }

            return result;
        }

        private static List<Dictionary<string, object>> ParseParameters(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var pair in ParsePairs(text))
            {
                result.Add(new Dictionary<string, object>
                {
                    { "Name", pair.Key },
                    { "Value", pair.Value }
                });
            }

            return result;
        }

        private static List<Dictionary<string, object>> ParseFieldMappings(string text)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var pair in ParsePairs(text))
            {
                result.Add(new Dictionary<string, object>
                {
                    { "FieldName", pair.Key },
                    { "Value", pair.Value }
                });
            }

            return result;
        }

        private static IList<KeyValuePair<string, string>> ParsePairs(string text)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var parts = text.Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var index = part.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                result.Add(new KeyValuePair<string, string>(
                    part.Substring(0, index).Trim(),
                    part.Substring(index + 1).Trim()));
            }

            return result;
        }

        private static string ToPairText(object value, string keyName, string valueName)
        {
            var list = value as System.Collections.IEnumerable;
            if (list == null || value is string)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            var parts = new List<string>();
            foreach (var item in list)
            {
                var dictionary = item as System.Collections.IDictionary;
                if (dictionary == null)
                {
                    continue;
                }

                var key = GetDictionaryValue(dictionary, keyName);
                var pairValue = GetDictionaryValue(dictionary, valueName);
                if (key != null)
                {
                    parts.Add(Convert.ToString(key, CultureInfo.InvariantCulture) + "=" + Convert.ToString(pairValue, CultureInfo.InvariantCulture));
                }
            }

            return string.Join(";", parts.ToArray());
        }

        private static object GetDictionaryValue(System.Collections.IDictionary dictionary, string key)
        {
            foreach (System.Collections.DictionaryEntry item in dictionary)
            {
                if (string.Equals(Convert.ToString(item.Key, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
                {
                    return item.Value;
                }
            }

            return null;
        }
    }
}
