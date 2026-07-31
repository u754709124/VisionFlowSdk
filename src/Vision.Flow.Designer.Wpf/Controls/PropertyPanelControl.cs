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
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 属性面板按 Descriptor 编辑节点配置；可绑定配置在固定值和结构化变量之间切换。
    /// </summary>
    public sealed class PropertyPanelControl : Border
    {
        private const double EditorErrorTextHeight = 28;
        private const double EditorStatusSlotHeight = 34;
        private readonly StackPanel _rows;
        private readonly Func<NodeSettingDescriptor, IEnumerable<NodeSettingConstantOption>> _constantOptionProvider;
        private readonly Button _applyButton;
        private readonly Button _resetButton;
        private readonly TextBlock _readOnlyHint;
        private readonly TextBlock _validationSummary;
        private readonly Dictionary<string, string> _editorErrors;
        private readonly Dictionary<string, TextBlock> _editorErrorBlocks;
        private readonly Dictionary<string, Control> _editorControls;
        private readonly Dictionary<string, Border> _editorErrorOutlines;
        private readonly Dictionary<string, string> _rawEditorTexts;
        private readonly ScrollViewer _scrollViewer;
        private Action _changed;
        private IList<VariableSelectionOption> _variableOptions;
        private bool _isReadOnly;
        private bool _establishEditorStateBaseline;
        private string _editorStateBaseline = string.Empty;
        private bool _hasPendingChanges;
        private string _renderedNodeId;
        private NodeDefinition _currentNode;
        private NodeDescriptor _currentDescriptor;
        private NodeExecutionPolicyPanelControl _executionPolicyPanel;

        public PropertyPanelControl()
            : this(null)
        {
        }

        public PropertyPanelControl(
            Func<NodeSettingDescriptor, IEnumerable<NodeSettingConstantOption>> constantOptionProvider)
        {
            _constantOptionProvider = constantOptionProvider;
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1, 0, 0, 0);
            CornerRadius = new CornerRadius(0);
            FlowDesignerTheme.ApplyTo(this);

            _rows = new StackPanel();
            _variableOptions = new List<VariableSelectionOption>();
            _editorErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _editorErrorBlocks = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            _editorControls = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            _editorErrorOutlines = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);
            _rawEditorTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var shell = new Grid();
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _rows
            };
            Grid.SetRow(_scrollViewer, 0);
            shell.Children.Add(_scrollViewer);

            var actionBar = new Border
            {
                Margin = new Thickness(-12, 12, -12, -12),
                Padding = new Thickness(12, 10, 12, 10),
                Background = FlowDesignerControl.BrushFromRgb(250, 251, 253),
                BorderBrush = FlowDesignerControl.BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            var actionLayout = new Grid();
            actionLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actionLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actionBar.Child = actionLayout;

            _validationSummary = new TextBlock
            {
                Foreground = FlowDesignerControl.BrushFromRgb(209, 67, 67),
                Height = EditorErrorTextHeight,
                LineHeight = 14,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 7),
                Visibility = Visibility.Hidden,
                Focusable = true,
                Tag = "PropertyValidationSummary"
            };
            _validationSummary.SetResourceReference(
                FrameworkElement.StyleProperty,
                FlowDesignerTheme.ErrorTextStyleKey);
            actionLayout.Children.Add(_validationSummary);

            var actionRow = new DockPanel();
            Grid.SetRow(actionRow, 1);
            actionLayout.Children.Add(actionRow);
            _readOnlyHint = new TextBlock
            {
                Text = "调试运行模式：属性只读",
                Foreground = FlowDesignerControl.BrushFromRgb(122, 135, 154),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            actionRow.Children.Add(_readOnlyHint);
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(buttons, Dock.Right);
            _resetButton = new Button
            {
                Content = "重置",
                Tag = "PropertyReset",
                MinWidth = 76,
                Height = 36,
                Margin = new Thickness(0, 0, 8, 0)
            };
            _resetButton.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.SecondaryButtonStyleKey);
            _resetButton.Click += delegate
            {
                var handler = ResetRequested;
                if (handler != null)
                {
                    handler();
                }
            };
            _applyButton = new Button
            {
                Content = "应用",
                Tag = "PropertyApply",
                MinWidth = 82,
                Height = 36
            };
            _applyButton.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.PrimaryButtonStyleKey);
            _applyButton.Click += delegate
            {
                var handler = ApplyRequested;
                if (handler != null)
                {
                    handler();
                }
            };
            buttons.Children.Add(_resetButton);
            buttons.Children.Add(_applyButton);
            actionRow.Children.Add(buttons);

            Grid.SetRow(actionBar, 1);
            shell.Children.Add(actionBar);
            Child = shell;
        }

        public event Action ApplyRequested;

        public event Action ResetRequested;

        public bool HasEditorErrors
        {
            get
            {
                return _editorErrors.Count > 0 ||
                    (_executionPolicyPanel != null && _executionPolicyPanel.HasValidationErrors);
            }
        }

        public bool HasUnappliedEditorState
        {
            get
            {
                return !string.Equals(
                    _editorStateBaseline,
                    CreateEditorStateSignature(),
                    StringComparison.Ordinal);
            }
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
            var nodeId = node == null ? null : node.Id;
            if (!string.Equals(_renderedNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            {
                ResetEditorState();
                _renderedNodeId = nodeId;
            }

            _changed = changed;
            _isReadOnly = isReadOnly;
            _currentNode = node;
            _currentDescriptor = descriptor;
            _variableOptions = variableOptions == null
                ? new List<VariableSelectionOption>()
                : variableOptions.Where(x => x != null && x.Selector != null).ToList();
            var executionPolicyParent = _executionPolicyPanel == null
                ? null
                : LogicalTreeHelper.GetParent(_executionPolicyPanel) as Border;
            if (executionPolicyParent != null &&
                object.ReferenceEquals(executionPolicyParent.Child, _executionPolicyPanel))
            {
                executionPolicyParent.Child = null;
            }
            _editorControls.Clear();
            _editorErrorBlocks.Clear();
            _editorErrorOutlines.Clear();
            _rows.Children.Clear();

            if (node == null)
            {
                _rows.Children.Add(CreateTitle("节点属性"));
                _rows.Children.Add(CreateMutedText("请在画布中选择一个节点。"));
                SetPendingState(false, isReadOnly);
                EstablishEditorStateBaselineIfRequested();
                return;
            }

            _rows.Children.Add(CreateNodeHeader(node, descriptor));

            var basicFields = new StackPanel();
            AddTextField(basicFields, "节点 ID", node.Id, false, null, "NodeId");
            AddTextField(basicFields, "节点名称", node.Name, true, delegate(string text) { node.Name = text; }, "NodeName");
            AddTextField(basicFields, "节点类型", node.Type, false, null, "NodeType");
            _rows.Children.Add(CreateSectionCard("基本信息", basicFields, true));

            var settingFields = new StackPanel();
            if (variableIssues != null)
            {
                foreach (var issue in variableIssues.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    settingFields.Children.Add(CreateInvalidText(issue));
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
                    }

                    AddSettingField(settingFields, setting, value, delegate(NodeSettingValue newValue)
                    {
                        node.Settings[setting.Name] = newValue;
                    });
                }
            }
            if (descriptor == null || descriptor.Settings == null || descriptor.Settings.Count == 0)
            {
                settingFields.Children.Add(CreateMutedText("该节点没有可配置参数。"));
            }
            _rows.Children.Add(CreateSectionCard("参数设置", settingFields, true));

            if (_executionPolicyPanel == null)
            {
                _executionPolicyPanel = new NodeExecutionPolicyPanelControl();
                _executionPolicyPanel.ValidationStateChanged += RefreshActionButtonState;
            }
            _executionPolicyPanel.ShowPolicy(node, descriptor, RaiseChanged, _isReadOnly);
            _rows.Children.Add(CreateSectionCard("执行策略", _executionPolicyPanel, true));

            if (descriptor != null && descriptor.Outputs.Count > 0)
            {
                var outputs = new StackPanel();
                foreach (var output in descriptor.Outputs)
                {
                    outputs.Children.Add(CreateOutputTag(output));
                }
                _rows.Children.Add(CreateSectionCard("输出", outputs, true));
            }

            SetPendingState(false, isReadOnly);
            EstablishEditorStateBaselineIfRequested();
        }

        private void AddSettingField(
            Panel layout,
            NodeSettingDescriptor setting,
            NodeSettingValue value,
            Action<NodeSettingValue> setter)
        {
            layout.Children.Add(CreateLabel(
                setting.DisplayName +
                (setting.IsRequired ? " *" : string.Empty) +
                " (" + setting.Name + ")"));
            var current = value ?? NodeSettingValue.ForConstant(setting.DefaultValue);
            if (setting.BindingMode != NodeSettingBindingMode.ConstantOrVariable ||
                setting.EvaluationPhase != NodeSettingEvaluationPhase.Execution)
            {
                if (current.Mode == NodeSettingValueMode.Variable)
                {
                    layout.Children.Add(CreateInvalidText(setting.EvaluationPhase == NodeSettingEvaluationPhase.ListenerStart
                        ? "该配置项在监听启动阶段求值，不能使用执行期变量；当前选择会保留并由校验器报告：" + VariableSelectionOption.FormatSelector(current.Selector)
                        : "该配置项只允许固定值；当前变量选择会保留并由校验器报告：" + VariableSelectionOption.FormatSelector(current.Selector)));
                }

                layout.Children.Add(CreateConstantEditor(setting, current.ConstantValue, delegate(object constantValue)
                {
                    ApplySetting(setter, NodeSettingValue.ForConstant(constantValue));
                }));
                return;
            }

            var container = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var modeSelector = new ComboBox
            {
                IsEnabled = !_isReadOnly,
                Tag = setting.Name + ":Mode",
                Visibility = Visibility.Collapsed
            };
            modeSelector.Items.Add(new ComboBoxItem { Content = "固定值", Tag = NodeSettingValueMode.Constant });
            modeSelector.Items.Add(new ComboBoxItem { Content = "变量", Tag = NodeSettingValueMode.Variable });
            modeSelector.SelectedIndex = current.Mode == NodeSettingValueMode.Variable ? 1 : 0;
            Grid.SetColumn(modeSelector, 0);
            container.Children.Add(modeSelector);

            var segments = new Grid
            {
                Height = 40,
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Top
            };
            segments.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            segments.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            segments.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var constantButton = CreateModeButton("固定值", current.Mode == NodeSettingValueMode.Constant);
            var variableButton = CreateModeButton("变量", current.Mode == NodeSettingValueMode.Variable);
            constantButton.IsEnabled = !_isReadOnly;
            variableButton.IsEnabled = !_isReadOnly;
            constantButton.Click += delegate { modeSelector.SelectedIndex = 0; };
            variableButton.Click += delegate { modeSelector.SelectedIndex = 1; };
            Grid.SetColumn(constantButton, 0);
            Grid.SetColumn(variableButton, 2);
            segments.Children.Add(constantButton);
            segments.Children.Add(variableButton);
            Grid.SetColumn(segments, 0);
            container.Children.Add(segments);

            var editorHost = new ContentControl
            {
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                VerticalContentAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(editorHost, 1);
            container.Children.Add(editorHost);

            Action renderEditor = null;
            renderEditor = delegate
            {
                if (current.Mode == NodeSettingValueMode.Variable)
                {
                    ClearEditorError("Setting:" + setting.Name, null);
                    editorHost.Content = CreateVariableEditor(setting, current, delegate(NodeSettingValue newValue)
                    {
                        current = newValue;
                        ApplySetting(setter, current);
                    });
                }
                else
                {
                    ClearEditorError("Variable:" + setting.Name, null);
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
                ApplyModeButtonVisual(constantButton, mode == NodeSettingValueMode.Constant);
                ApplyModeButtonVisual(variableButton, mode == NodeSettingValueMode.Variable);
                RaiseChanged();
                renderEditor();
            };

            renderEditor();
            layout.Children.Add(container);
        }

        private UIElement CreateVariableEditor(NodeSettingDescriptor setting, NodeSettingValue value, Action<NodeSettingValue> setter)
        {
            var allowedOptions = _variableOptions
                .Where(x => IsSourceAllowed(setting.AllowedVariableSources, x.Selector.Scope))
                .ToList();
            var compatibleOptions = allowedOptions
                .Where(x => FlowDataTypeCompatibility.IsCompatible(
                    x.DataType,
                    x.EnumType,
                    setting.DataType,
                    setting.EnumType))
                .ToList();

            var layout = new StackPanel();
            var selector = new VariableSelectorControl(compatibleOptions)
            {
                IsEnabled = !_isReadOnly
            };
            selector.ShowSelector(value.Selector);
            var editorKey = "Variable:" + setting.Name;
            _editorControls[editorKey] = selector;
            var selectorFrame = new Grid();
            selectorFrame.Children.Add(selector);
            var selectorOutline = CreateValidationOutline(editorKey, selector);
            Panel.SetZIndex(selectorOutline, 1);
            selectorFrame.Children.Add(selectorOutline);
            _editorErrorOutlines[editorKey] = selectorOutline;
            layout.Children.Add(selectorFrame);

            var status = CreateVariableStatus(setting, value.Selector, allowedOptions, compatibleOptions);
            var statusHost = new ContentControl
            {
                Height = EditorStatusSlotHeight,
                ClipToBounds = true,
                Tag = "VariableStatus:" + setting.Name,
                ToolTip = GetStatusText(status),
                Content = status
            };
            layout.Children.Add(statusHost);
            UpdateVariableEditorStatus(
                setting,
                value.Selector,
                allowedOptions,
                compatibleOptions,
                selector,
                statusHost);

            selector.VariableSelected += delegate(VariableSelectionOption selected)
            {
                if (!_isReadOnly && selected != null)
                {
                    value = NodeSettingValue.ForVariable(CloneSelector(selected.Selector), value.ConstantValue);
                    setter(value);
                    UpdateVariableEditorStatus(
                        setting,
                        value.Selector,
                        allowedOptions,
                        compatibleOptions,
                        selector,
                        statusHost);
                }
            };
            return layout;
        }

        private void UpdateVariableEditorStatus(
            NodeSettingDescriptor setting,
            VariableSelector selectorValue,
            IList<VariableSelectionOption> allowedOptions,
            IList<VariableSelectionOption> compatibleOptions,
            VariableSelectorControl selector,
            ContentControl statusHost)
        {
            var status = CreateVariableStatus(
                setting,
                selectorValue,
                allowedOptions,
                compatibleOptions);
            statusHost.Content = status;
            statusHost.ToolTip = GetStatusText(status);

            var editorKey = "Variable:" + setting.Name;
            var error = GetVariableValidationError(
                setting,
                selectorValue,
                allowedOptions,
                compatibleOptions);
            if (string.IsNullOrWhiteSpace(error))
            {
                ClearEditorError(editorKey, selector);
                selector.ShowSelector(selectorValue);
            }
            else
            {
                SetEditorError(editorKey, error, selector);
            }
        }

        private static string GetStatusText(UIElement status)
        {
            var textBlock = status as TextBlock;
            return textBlock == null ? null : textBlock.Text;
        }

        private UIElement CreateConstantEditor(NodeSettingDescriptor setting, object value, Action<object> setter)
        {
            var editorKey = "Setting:" + setting.Name;
            if (setting.DataType == FlowDataType.Boolean)
            {
                var checkBox = new CheckBox
                {
                    Content = "启用",
                    IsChecked = value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                    IsEnabled = !_isReadOnly,
                    Height = 40,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                checkBox.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.SwitchCheckBoxStyleKey);
                Action<bool> applyValue = delegate(bool nextValue)
                {
                    if (_isReadOnly)
                    {
                        return;
                    }

                    object normalizedValue;
                    string validationError;
                    if (!NodeSettingValueValidation.TryValidateConstant(
                        setting,
                        nextValue,
                        out normalizedValue,
                        out validationError))
                    {
                        SetEditorError(editorKey, validationError, checkBox);
                        RaiseChanged();
                        return;
                    }

                    ClearEditorError(editorKey, checkBox);
                    setter(normalizedValue);
                };
                checkBox.Checked += delegate { applyValue(true); };
                checkBox.Unchecked += delegate { applyValue(false); };
                object initialBooleanValue;
                string initialBooleanError;
                if (!NodeSettingValueValidation.TryValidateConstant(
                    setting,
                    checkBox.IsChecked == true,
                    out initialBooleanValue,
                    out initialBooleanError))
                {
                    SetEditorError(editorKey, initialBooleanError, checkBox);
                }
                return WrapEditorWithError(editorKey, checkBox);
            }

            bool usesHostOptions;
            var selectorItems = GetSelectorItems(setting, out usesHostOptions);
            if (usesHostOptions)
            {
                var initialText = GetRawEditorText(editorKey, ToEditorText(setting, value));
                var initialOption = selectorItems.FirstOrDefault(x =>
                    string.Equals(x.Value, initialText, StringComparison.OrdinalIgnoreCase));
                var comboBox = new ComboBox
                {
                    IsEditable = false,
                    IsEnabled = !_isReadOnly,
                    Text = initialText,
                    Tag = editorKey
                };
                comboBox.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.FieldComboBoxStyleKey);
                foreach (var item in selectorItems)
                {
                    comboBox.Items.Add(item);
                }
                comboBox.SelectedItem = initialOption;
                if (initialOption == null)
                    comboBox.Text = initialText;
                if (usesHostOptions &&
                    !string.IsNullOrWhiteSpace(initialText) &&
                    initialOption == null)
                {
                    SetEditorError(editorKey, setting.DisplayName + " 的当前候选项已失效。", comboBox);
                }
                else
                {
                    object initialValue;
                    string initialError;
                    if (!TryConvertFromEditorText(setting, initialText, out initialValue, out initialError))
                    {
                        SetEditorError(editorKey, initialError, comboBox);
                    }
                    else
                    {
                        ClearEditorError(editorKey, comboBox);
                    }
                }

                Action applyComboValue = delegate
                {
                    if (_isReadOnly)
                    {
                        return;
                    }

                    var selectedOption = comboBox.SelectedItem as NodeSettingConstantOption;
                    var text = selectedOption == null ? string.Empty : selectedOption.Value;
                    _rawEditorTexts[editorKey] = text;
                    if (selectedOption == null)
                    {
                        SetEditorError(editorKey, "请选择有效候选项。", comboBox);
                        RaiseChanged();
                        return;
                    }

                    object converted;
                    string conversionError;
                    if (!TryConvertFromEditorText(setting, text, out converted, out conversionError))
                    {
                        SetEditorError(editorKey, conversionError, comboBox);
                        RaiseChanged();
                        return;
                    }

                    ClearEditorError(editorKey, comboBox);
                    setter(converted);
                };
                comboBox.LostFocus += delegate { applyComboValue(); };
                comboBox.DropDownClosed += delegate { applyComboValue(); };
                comboBox.SelectionChanged += delegate { applyComboValue(); };
                return WrapEditorWithError(editorKey, comboBox);
            }

            var formattedValue = ToEditorText(setting, value);
            var acceptsMultiline = IsMultilineSetting(setting);
            var textBox = new TextBox
            {
                Text = GetRawEditorText(editorKey, formattedValue),
                IsReadOnly = _isReadOnly,
                TextWrapping = acceptsMultiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                AcceptsReturn = acceptsMultiline,
                Tag = editorKey
            };
            textBox.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.FieldTextBoxStyleKey);
            if (acceptsMultiline)
            {
                textBox.MinHeight = 76;
                textBox.Padding = new Thickness(11, 8, 11, 8);
                textBox.VerticalContentAlignment = VerticalAlignment.Top;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                textBox.Height = 40;
                textBox.VerticalContentAlignment = VerticalAlignment.Center;
                textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }

            textBox.TextChanged += delegate
            {
                if (_isReadOnly)
                {
                    return;
                }

                _rawEditorTexts[editorKey] = textBox.Text ?? string.Empty;
                object converted;
                string conversionError;
                if (!TryConvertFromEditorText(setting, textBox.Text, out converted, out conversionError))
                {
                    SetEditorError(editorKey, conversionError, textBox);
                    RaiseChanged();
                    return;
                }

                ClearEditorError(editorKey, textBox);
                setter(converted);
            };
            object initialConverted;
            string initialConversionError;
            if (!TryConvertFromEditorText(setting, textBox.Text, out initialConverted, out initialConversionError))
            {
                SetEditorError(editorKey, initialConversionError, textBox);
            }
            else
            {
                ClearEditorError(editorKey, textBox);
            }
            return WrapEditorWithError(editorKey, textBox);
        }

        private static bool IsMultilineSetting(NodeSettingDescriptor setting)
        {
            return setting != null &&
                !string.IsNullOrWhiteSpace(setting.Name) &&
                (setting.Name.IndexOf("Mappings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 setting.Name.IndexOf("Channels", StringComparison.OrdinalIgnoreCase) >= 0);
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
                var sourceType = source.EnumType == null
                    ? FlowEnumConverter.ToWireValue(source.DataType)
                    : source.EnumType.Name;
                var targetType = setting.EnumType == null
                    ? FlowEnumConverter.ToWireValue(setting.DataType)
                    : setting.EnumType.Name;
                return CreateInvalidText("变量类型 " + sourceType +
                    " 不能赋给 " + targetType + "。");
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
                case VariableSelectorScope.EnvironmentVariable:
                    return (flags & VariableSelectorScopeFlags.EnvironmentVariable) != 0;
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

        private void AddTextField(
            Panel layout,
            string label,
            string value,
            bool editable,
            Action<string> setter,
            string editorKey)
        {
            layout.Children.Add(CreateLabel(label));
            var textBox = new TextBox
            {
                Text = editable
                    ? GetRawEditorText(editorKey, value ?? string.Empty)
                    : value ?? string.Empty,
                IsReadOnly = !editable || _isReadOnly,
                TextWrapping = TextWrapping.Wrap,
                Tag = editorKey
            };
            textBox.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.FieldTextBoxStyleKey);
            textBox.TextChanged += delegate
            {
                if (_isReadOnly || !editable || setter == null)
                {
                    return;
                }

                _rawEditorTexts[editorKey] = textBox.Text ?? string.Empty;
                setter(textBox.Text);
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    SetEditorError(editorKey, "节点名称不能为空。", textBox);
                }
                else
                {
                    ClearEditorError(editorKey, textBox);
                }

                RaiseChanged();
            };
            layout.Children.Add(editable ? WrapEditorWithError(editorKey, textBox) : textBox);
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
            RefreshActionButtonState();
            if (_changed != null)
            {
                _changed();
            }
        }

        public bool TryValidate(out string error)
        {
            error = null;
            if (_editorErrors.Count > 0)
            {
                error = _editorErrors.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                FocusFirstEditorError();
                return false;
            }

            if (_currentNode == null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_currentNode.Name))
            {
                error = "节点名称不能为空。";
                SetEditorError("NodeName", error, null);
                FocusFirstEditorError();
                return false;
            }

            if (_currentDescriptor != null && _currentDescriptor.Settings != null)
            {
                foreach (var setting in _currentDescriptor.Settings.Where(x => x != null))
                {
                    NodeSettingValue value;
                    _currentNode.Settings.TryGetValue(setting.Name, out value);
                    if (value == null)
                    {
                        value = NodeSettingValue.ForConstant(setting.DefaultValue);
                    }

                    if (value.Mode == NodeSettingValueMode.Variable)
                    {
                        var allowed = _variableOptions
                            .Where(x => IsSourceAllowed(setting.AllowedVariableSources, x.Selector.Scope))
                            .ToList();
                        var compatible = allowed
                            .Where(x => FlowDataTypeCompatibility.IsCompatible(x.DataType, setting.DataType))
                            .ToList();
                        var variableError = GetVariableValidationError(setting, value.Selector, allowed, compatible);
                        if (!string.IsNullOrWhiteSpace(variableError))
                        {
                            error = variableError;
                            ShowValidationError(error);
                            FocusValidationSummary();
                            return false;
                        }

                        continue;
                    }

                    if (setting.IsRequired &&
                        (value.ConstantValue == null ||
                         (value.ConstantValue is string && string.IsNullOrWhiteSpace((string)value.ConstantValue))))
                    {
                        error = setting.DisplayName + " 为必填项。";
                        SetEditorError("Setting:" + setting.Name, error, null);
                        FocusFirstEditorError();
                        return false;
                    }

                    if (_constantOptionProvider != null)
                    {
                        var provided = _constantOptionProvider(setting);
                        if (provided != null)
                        {
                            var candidates = provided
                                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Value))
                                .Select(x => x.Value)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                            var text = Convert.ToString(value.ConstantValue, CultureInfo.InvariantCulture);
                            if (!string.IsNullOrWhiteSpace(text) &&
                                !candidates.Contains(text, StringComparer.OrdinalIgnoreCase))
                            {
                                error = setting.DisplayName + " 的当前候选项已失效。";
                                SetEditorError("Setting:" + setting.Name, error, null);
                                FocusFirstEditorError();
                                return false;
                            }
                        }
                    }
                }
            }

            if (_executionPolicyPanel != null && !_executionPolicyPanel.TryValidate(out error))
            {
                return false;
            }

            return true;
        }

        public void SetPendingState(bool hasPending, bool isReadOnly)
        {
            _hasPendingChanges = hasPending;
            _isReadOnly = isReadOnly;
            RefreshActionButtonState();
            _resetButton.IsEnabled = hasPending && !isReadOnly && _currentNode != null;
            _applyButton.Visibility = isReadOnly ? Visibility.Collapsed : Visibility.Visible;
            _resetButton.Visibility = isReadOnly ? Visibility.Collapsed : Visibility.Visible;
            _readOnlyHint.Visibility = isReadOnly && _currentNode != null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RefreshActionButtonState()
        {
            _applyButton.IsEnabled =
                _hasPendingChanges &&
                !_isReadOnly &&
                _currentNode != null &&
                !HasEditorErrors;
        }

        public void ShowValidationError(string error)
        {
            _validationSummary.Text = error ?? string.Empty;
            _validationSummary.ToolTip = string.IsNullOrWhiteSpace(error) ? null : error;
            _validationSummary.Visibility = string.IsNullOrWhiteSpace(error)
                ? Visibility.Hidden
                : Visibility.Visible;
        }

        public void FocusValidationSummary()
        {
            _validationSummary.BringIntoView();
            _validationSummary.Focus();
        }

        public void ResetEditorState()
        {
            _editorErrors.Clear();
            _editorErrorBlocks.Clear();
            _editorControls.Clear();
            _editorErrorOutlines.Clear();
            _rawEditorTexts.Clear();
            _renderedNodeId = null;
            _establishEditorStateBaseline = true;
            if (_executionPolicyPanel != null)
            {
                _executionPolicyPanel.ResetEditorState();
            }
            ShowValidationError(null);
            RefreshActionButtonState();
        }

        private string CreateEditorStateSignature()
        {
            IEnumerable<string> propertyState = _editorErrors
                .Select(x => "E:" + x.Key + "=" + x.Value)
                .Concat(_rawEditorTexts.Select(x => "R:" + x.Key + "=" + x.Value));
            string executionPolicyState = _executionPolicyPanel == null
                ? string.Empty
                : _executionPolicyPanel.EditorStateSignature;
            return string.Join(
                "\n",
                propertyState
                    .Concat(new[] { "P:" + executionPolicyState })
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray());
        }

        private void EstablishEditorStateBaselineIfRequested()
        {
            if (!_establishEditorStateBaseline)
            {
                return;
            }

            _editorStateBaseline = CreateEditorStateSignature();
            _establishEditorStateBaseline = false;
        }

        internal void RemoveDescriptorEditorState(
            IEnumerable<string> settingNames,
            IEnumerable<string> outputNames)
        {
            foreach (var settingName in settingNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(settingName))
                {
                    continue;
                }

                RemoveEditorState("Setting:" + settingName);
                RemoveEditorState("Variable:" + settingName);
                RemoveEditorState(settingName + ":Mode");
            }

            if (_executionPolicyPanel != null)
            {
                _executionPolicyPanel.RemoveDefaultOutputEditorState(outputNames);
            }

            RefreshActionButtonState();
        }

        private void RemoveEditorState(string key)
        {
            _editorErrors.Remove(key);
            _rawEditorTexts.Remove(key);
            _editorControls.Remove(key);
            _editorErrorBlocks.Remove(key);
            _editorErrorOutlines.Remove(key);
        }

        public void UpdateNodeName(string name)
        {
            if (_currentNode == null || _isReadOnly)
            {
                return;
            }

            var value = string.IsNullOrWhiteSpace(name) ? _currentNode.Id : name.Trim();
            _currentNode.Name = value;
            _rawEditorTexts["NodeName"] = value;
            ClearEditorError("NodeName", null);
            RaiseChanged();
        }

        private string GetRawEditorText(string key, string fallback)
        {
            string raw;
            return !string.IsNullOrWhiteSpace(key) && _rawEditorTexts.TryGetValue(key, out raw)
                ? raw
                : fallback;
        }

        private UIElement WrapEditorWithError(string key, FrameworkElement editor)
        {
            var layout = new StackPanel();
            var editorFrame = new Grid();
            editorFrame.Children.Add(editor);
            var errorOutline = CreateValidationOutline(key, editor);
            Panel.SetZIndex(errorOutline, 1);
            editorFrame.Children.Add(errorOutline);
            layout.Children.Add(editorFrame);
            var control = editor as Control;
            if (control != null && !string.IsNullOrWhiteSpace(key))
            {
                _editorControls[key] = control;
            }
            var error = new TextBlock
            {
                Foreground = FlowDesignerControl.BrushFromRgb(209, 67, 67),
                FontSize = 11,
                Height = EditorErrorTextHeight,
                LineHeight = 14,
                Margin = new Thickness(1, 3, 0, 3),
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Tag = "EditorError:" + (key ?? string.Empty),
                Visibility = Visibility.Hidden
            };
            error.SetResourceReference(
                FrameworkElement.StyleProperty,
                FlowDesignerTheme.ErrorTextStyleKey);
            string existing;
            if (_editorErrors.TryGetValue(key, out existing) && !string.IsNullOrWhiteSpace(existing))
            {
                error.Text = existing;
                error.ToolTip = existing;
                error.Visibility = Visibility.Visible;
                errorOutline.Visibility = Visibility.Visible;
                if (control != null)
                {
                    control.BorderBrush = FlowDesignerControl.BrushFromRgb(209, 67, 67);
                    control.ToolTip = existing;
                }
            }

            _editorErrorBlocks[key] = error;
            _editorErrorOutlines[key] = errorOutline;
            layout.Children.Add(error);
            return layout;
        }

        internal static Border CreateValidationOutline(string key, FrameworkElement editor)
        {
            var outline = new Border
            {
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                IsHitTestVisible = false,
                Margin = editor == null ? new Thickness(0) : editor.Margin,
                SnapsToDevicePixels = true,
                Tag = "ValidationOutline:" + (key ?? string.Empty),
                Visibility = Visibility.Collapsed
            };
            outline.SetResourceReference(
                Border.BorderBrushProperty,
                FlowDesignerTheme.ErrorBrushKey);
            return outline;
        }

        private void SetEditorError(string key, string error, Control editor)
        {
            _editorErrors[key] = error;
            TextBlock block;
            if (_editorErrorBlocks.TryGetValue(key, out block))
            {
                block.Text = error;
                block.ToolTip = error;
                block.Visibility = Visibility.Visible;
            }
            Border outline;
            if (_editorErrorOutlines.TryGetValue(key, out outline))
            {
                outline.Visibility = Visibility.Visible;
            }

            if (editor == null)
            {
                _editorControls.TryGetValue(key, out editor);
            }

            if (editor != null)
            {
                editor.BorderBrush = FlowDesignerControl.BrushFromRgb(209, 67, 67);
                editor.ToolTip = error;
            }

            RefreshActionButtonState();
        }

        private void ClearEditorError(string key, Control editor)
        {
            _editorErrors.Remove(key);
            TextBlock block;
            if (_editorErrorBlocks.TryGetValue(key, out block))
            {
                block.Text = string.Empty;
                block.ToolTip = null;
                block.Visibility = Visibility.Hidden;
            }
            Border outline;
            if (_editorErrorOutlines.TryGetValue(key, out outline))
            {
                outline.Visibility = Visibility.Collapsed;
            }

            if (editor == null)
            {
                _editorControls.TryGetValue(key, out editor);
            }

            if (editor != null)
            {
                editor.ClearValue(Control.BorderBrushProperty);
                editor.ToolTip = null;
            }

            RefreshActionButtonState();
        }

        private void FocusFirstEditorError()
        {
            foreach (var key in _editorErrors.Keys)
            {
                Control editor;
                if (_editorControls.TryGetValue(key, out editor))
                {
                    editor.BringIntoView();
                    editor.Focus();
                    return;
                }
            }

            FocusValidationSummary();
        }

        private static Button CreateModeButton(string text, bool isSelected)
        {
            var button = new Button
            {
                Content = text,
                Height = 40,
                Padding = new Thickness(5, 0, 5, 0),
                BorderThickness = new Thickness(1),
                FontSize = 11.5,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            button.SetResourceReference(
                FrameworkElement.StyleProperty,
                FlowDesignerTheme.SegmentButtonStyleKey);
            ApplyModeButtonVisual(button, isSelected);
            return button;
        }

        private static void ApplyModeButtonVisual(Button button, bool isSelected)
        {
            button.Background = isSelected
                ? FlowDesignerControl.BrushFromRgb(234, 248, 242)
                : Brushes.White;
            button.Foreground = isSelected
                ? FlowDesignerControl.BrushFromRgb(13, 139, 97)
                : FlowDesignerControl.BrushFromRgb(100, 116, 139);
            button.BorderBrush = isSelected
                ? FlowDesignerControl.BrushFromRgb(16, 163, 114)
                : FlowDesignerControl.BrushFromRgb(221, 229, 239);
            button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
            button.Margin = new Thickness(0);
        }

        private static UIElement CreateNodeHeader(NodeDefinition node, NodeDescriptor descriptor)
        {
            var border = new Border
            {
                Background = FlowDesignerControl.BrushFromRgb(247, 250, 252),
                BorderBrush = FlowDesignerControl.BrushFromRgb(221, 229, 239),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            border.SetResourceReference(
                FrameworkElement.StyleProperty,
                FlowDesignerTheme.CardBorderStyleKey);
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            border.Child = row;
            var icon = new Border
            {
                Width = 34,
                Height = 34,
                Background = FlowDesignerControl.BrushFromRgb(234, 248, 242),
                CornerRadius = new CornerRadius(7),
                Child = FlowDesignerIcons.CreateNode(
                    node.Type,
                    FlowDesignerControl.BrushFromRgb(16, 163, 114),
                    18)
            };
            row.Children.Add(icon);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = FlowDesignerControl.BrushFromRgb(36, 50, 71),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new TextBlock
            {
                Text = descriptor == null || string.IsNullOrWhiteSpace(descriptor.Description)
                    ? node.Type
                    : descriptor.Description,
                FontSize = 10.5,
                Foreground = FlowDesignerControl.BrushFromRgb(122, 135, 154),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return border;
        }

        private static UIElement CreateSectionCard(string title, UIElement content, bool isExpanded)
        {
            var contentBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = FlowDesignerControl.BrushFromRgb(226, 232, 240),
                BorderThickness = new Thickness(1, 0, 1, 1),
                CornerRadius = new CornerRadius(0, 0, 7, 7),
                Padding = new Thickness(12, 4, 12, 12),
                Child = content
            };
            contentBorder.SetResourceReference(
                FrameworkElement.StyleProperty,
                FlowDesignerTheme.CardBorderStyleKey);
            var expander = new Expander
            {
                Header = new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = FlowDesignerControl.BrushFromRgb(51, 65, 85)
                },
                Content = contentBorder,
                IsExpanded = isExpanded,
                Margin = new Thickness(0, 0, 0, 9),
                Tag = "PropertySection:" + title
            };
            expander.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.ExpanderStyleKey);
            return expander;
        }

        private static UIElement CreateOutputTag(NodeOutputDescriptor output)
        {
            var border = new Border
            {
                Background = FlowDesignerControl.BrushFromRgb(245, 247, 250),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 4, 0, 0)
            };
            var row = new DockPanel();
            border.Child = row;
            var type = new TextBlock
            {
                Text = FlowEnumConverter.ToWireValue(output.DataType),
                Foreground = FlowDesignerControl.BrushFromRgb(47, 128, 237),
                FontSize = 10.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(type, Dock.Right);
            row.Children.Add(type);
            row.Children.Add(new TextBlock
            {
                Text = (string.IsNullOrWhiteSpace(output.DisplayName) ? output.Name : output.DisplayName) +
                    " (" + output.Name + ")",
                Foreground = FlowDesignerControl.BrushFromRgb(75, 91, 112),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            return border;
        }

        private static string GetVariableValidationError(
            NodeSettingDescriptor setting,
            VariableSelector selector,
            IList<VariableSelectionOption> allowedOptions,
            IList<VariableSelectionOption> compatibleOptions)
        {
            if (selector == null || selector.Path == null || selector.Path.Count == 0)
            {
                return "请选择变量。";
            }

            if (!IsSourceAllowed(setting.AllowedVariableSources, selector.Scope))
            {
                return "当前配置项不允许该变量范围。";
            }

            var source = allowedOptions.FirstOrDefault(x => x.Matches(selector));
            if (source == null)
            {
                return "变量来源不可用：" + VariableSelectionOption.FormatSelector(selector);
            }

            return compatibleOptions.Any(x => x.Matches(selector))
                ? null
                : "变量类型不能赋给 " + FlowEnumConverter.ToWireValue(setting.DataType) + "。";
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
                LineHeight = 14,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                ToolTip = text,
                Margin = new Thickness(0, 3, 0, 3)
            };
        }

        private IList<NodeSettingConstantOption> GetSelectorItems(NodeSettingDescriptor setting, out bool usesHostOptions)
        {
            var items = new List<NodeSettingConstantOption>();
            usesHostOptions = false;
            if (setting == null)
            {
                return items;
            }

            if (_constantOptionProvider != null)
            {
                var providedOptions = _constantOptionProvider(setting);
                if (providedOptions != null)
                {
                    usesHostOptions = true;
                    foreach (var option in providedOptions.Where(x => x != null))
                    {
                        if (!items.Any(x => string.Equals(
                            x.Value,
                            option.Value,
                            StringComparison.OrdinalIgnoreCase)))
                        {
                            items.Add(option);
                        }
                    }
                }
            }

            if (!usesHostOptions &&
                setting.EnumType != null &&
                setting.EnumType.IsEnum)
            {
                usesHostOptions = true;
                foreach (var name in Enum.GetNames(setting.EnumType))
                {
                    items.Add(new NodeSettingConstantOption(name, name));
                }
            }

            return items;
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

        private static bool TryConvertFromEditorText(
            NodeSettingDescriptor setting,
            string text,
            out object value,
            out string error)
        {
            object converted;
            if (!TryConvertEditorTextCore(setting, text, out converted, out error))
            {
                value = null;
                return false;
            }

            return NodeSettingValueValidation.TryValidateConstant(
                setting,
                converted,
                out value,
                out error);
        }

        private static bool TryConvertEditorTextCore(
            NodeSettingDescriptor setting,
            string text,
            out object value,
            out string error)
        {
            value = null;
            error = null;
            if (setting == null)
            {
                value = text;
                return true;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                if (setting.IsRequired)
                {
                    error = setting.DisplayName + " 为必填项。";
                    return false;
                }

                value = null;
                return true;
            }

            if (string.Equals(setting.Name, "Channels", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryValidatePairText(text, true, out error))
                {
                    return false;
                }

                value = ParseChannels(text);
                return true;
            }

            if (string.Equals(setting.Name, "Parameters", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryValidatePairText(text, false, out error))
                {
                    return false;
                }

                value = ParseParameters(text);
                return true;
            }

            if (string.Equals(setting.Name, "FieldMappings", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryValidatePairText(text, false, out error))
                {
                    return false;
                }

                value = ParseFieldMappings(text);
                return true;
            }

            if (setting.DataType == FlowDataType.Int32)
            {
                int intValue;
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                {
                    error = "请输入有效整数。";
                    return false;
                }

                value = intValue;
                return true;
            }

            if (setting.DataType == FlowDataType.Int64)
            {
                long longValue;
                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue))
                {
                    error = "请输入有效长整数。";
                    return false;
                }

                value = longValue;
                return true;
            }

            if (setting.DataType == FlowDataType.Double)
            {
                double doubleValue;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue))
                {
                    error = "请输入有效数字。";
                    return false;
                }

                value = doubleValue;
                return true;
            }

            if (setting.DataType == FlowDataType.Boolean)
            {
                bool boolValue;
                if (!bool.TryParse(text, out boolValue))
                {
                    error = "请输入 true 或 false。";
                    return false;
                }

                value = boolValue;
                return true;
            }

            if (setting.DataType == FlowDataType.DateTime)
            {
                DateTime dateTime;
                if (!DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out dateTime))
                {
                    error = "请输入 ISO 8601 日期时间。";
                    return false;
                }

                value = dateTime;
                return true;
            }

            value = text;
            return true;
        }

        private static bool TryValidatePairText(string text, bool numericValue, out string error)
        {
            error = null;
            var parts = (text ?? string.Empty)
                .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var index = part.IndexOf('=');
                if (index <= 0 || index >= part.Length - 1)
                {
                    error = "每一项都必须使用 名称=值 格式。";
                    return false;
                }

                if (numericValue)
                {
                    double parsed;
                    if (!double.TryParse(
                            part.Substring(index + 1).Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out parsed))
                    {
                        error = "通道强度必须是有效数字。";
                        return false;
                    }
                }
            }

            return true;
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
