using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 调试入口面板负责入口选择、手动输入采集，以及外部和节点事件入口的只读说明。
    /// 面板中的输入值仅用于本次设计器调试，不写入流程文件。
    /// </summary>
    public sealed class EntryTriggerPanelControl : Border
    {
        private readonly StackPanel _rows;
        private readonly Dictionary<string, Dictionary<string, object>> _valuesByEntry;
        private IList<FlowEntryDefinition> _entries;
        private ComboBox _entrySelector;
        private bool _isReadOnly;

        public EntryTriggerPanelControl()
        {
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);
            MaxHeight = 300;

            _entries = new List<FlowEntryDefinition>();
            _valuesByEntry = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            _rows = new StackPanel();
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _rows
            };
        }

        /// <summary>
        /// 当前选择的入口；没有入口时为空。
        /// </summary>
        public FlowEntryDefinition SelectedEntry { get; private set; }

        /// <summary>
        /// 入口选择发生变化时触发。
        /// </summary>
        public event Action<FlowEntryDefinition> EntrySelected;

        /// <summary>
        /// 重置当前入口选择和未持久化的手动输入值。
        /// </summary>
        public void Reset()
        {
            SelectedEntry = null;
            _valuesByEntry.Clear();
            _rows.Children.Clear();
        }

        /// <summary>
        /// 展示运行态定义中的入口，并恢复同一入口此前填写的调试值。
        /// </summary>
        public void ShowEntries(
            IEnumerable<FlowEntryDefinition> entries,
            string selectedEntryName,
            bool isReadOnly)
        {
            _entries = entries == null
                ? new List<FlowEntryDefinition>()
                : entries.Where(x => x != null).ToList();
            _isReadOnly = isReadOnly;
            SelectedEntry = FindSelectedEntry(selectedEntryName);
            Render();
        }

        /// <summary>
        /// 根据当前表单创建手动触发请求；非手动入口只展示信息，不允许由设计器触发。
        /// </summary>
        public bool TryCreateManualRequest(FlowToken token, out FlowTriggerRequest request, out string error)
        {
            request = null;
            error = null;
            var entry = SelectedEntry;
            if (entry == null)
            {
                error = "请选择一个手动入口。";
                return false;
            }

            if (entry.TriggerKind != FlowTriggerKind.Manual)
            {
                error = "入口 '" + entry.EntryName + "' 是" + GetTriggerKindDisplayName(entry.TriggerKind) + "，不能由设计器手动触发。";
                return false;
            }

            var inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var values = GetEntryValues(entry.EntryName);
            foreach (var input in entry.Inputs ?? new List<TriggerInputDescriptor>())
            {
                if (input == null || string.IsNullOrWhiteSpace(input.Name))
                {
                    continue;
                }

                object rawValue;
                values.TryGetValue(input.Name, out rawValue);
                if (IsEmpty(rawValue))
                {
                    rawValue = input.DefaultValue;
                }

                if (IsEmpty(rawValue))
                {
                    if (input.IsRequired)
                    {
                        error = "手动入口缺少必填输入：" + GetInputLabel(input) + "。";
                        return false;
                    }

                    continue;
                }

                object converted;
                if (!TryConvertValue(rawValue, input.DataType, out converted, out error))
                {
                    error = GetInputLabel(input) + "：" + error;
                    return false;
                }

                inputs[input.Name] = converted;
            }

            request = new FlowTriggerRequest
            {
                EntryName = entry.EntryName,
                Source = FlowTriggerSource.Manual,
                Token = token,
                Inputs = inputs
            };
            return true;
        }

        private FlowEntryDefinition FindSelectedEntry(string selectedEntryName)
        {
            var selected = _entries.FirstOrDefault(x =>
                string.Equals(x.EntryName, selectedEntryName, StringComparison.OrdinalIgnoreCase));
            return selected ?? _entries.FirstOrDefault(x => x.TriggerKind == FlowTriggerKind.Manual) ?? _entries.FirstOrDefault();
        }

        private void Render()
        {
            _rows.Children.Clear();
            _rows.Children.Add(CreateTitle("调试入口"));
            if (_entries.Count == 0)
            {
                _rows.Children.Add(CreateStatusText("当前流程没有入口。", FlowDesignerControl.BrushFromRgb(185, 28, 28)));
                return;
            }

            _rows.Children.Add(CreateLabel("入口"));
            _entrySelector = new ComboBox
            {
                MinHeight = 28,
                IsEnabled = !_isReadOnly,
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225),
                Tag = "DebugEntrySelector"
            };
            foreach (var entry in _entries)
            {
                _entrySelector.Items.Add(new ComboBoxItem
                {
                    Content = entry.EntryName + " [" + GetTriggerKindDisplayName(entry.TriggerKind) + "]",
                    Tag = entry
                });
            }

            _entrySelector.SelectedItem = _entrySelector.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(x => object.ReferenceEquals(x.Tag, SelectedEntry));
            _entrySelector.SelectionChanged += delegate
            {
                var selectedItem = _entrySelector.SelectedItem as ComboBoxItem;
                var selected = selectedItem == null ? null : selectedItem.Tag as FlowEntryDefinition;
                if (selected == null || object.ReferenceEquals(selected, SelectedEntry))
                {
                    return;
                }

                SelectedEntry = selected;
                Render();
                var handler = EntrySelected;
                if (handler != null)
                {
                    handler(selected);
                }
            };
            _rows.Children.Add(_entrySelector);

            RenderSelectedEntry();
        }

        private void RenderSelectedEntry()
        {
            var entry = SelectedEntry;
            if (entry == null)
            {
                return;
            }

            _rows.Children.Add(CreateStatusText(
                "类型：" + GetTriggerKindDisplayName(entry.TriggerKind) + "  ·  目标：" + (entry.TargetNodeId ?? "-"),
                FlowDesignerControl.BrushFromRgb(71, 85, 105)));
            if (entry.TriggerKind == FlowTriggerKind.NodeEvent)
            {
                _rows.Children.Add(CreateStatusText(
                    "监听源节点：" + (string.IsNullOrWhiteSpace(entry.SourceNodeId) ? "未配置" : entry.SourceNodeId),
                    FlowDesignerControl.BrushFromRgb(71, 85, 105)));
            }

            var policy = entry.ExecutionPolicy ?? new TriggerExecutionPolicy();
            _rows.Children.Add(CreateStatusText(
                "并发：" + policy.MaxConcurrentRuns.ToString(CultureInfo.InvariantCulture) +
                "  ·  等待队列：" + policy.QueueCapacity.ToString(CultureInfo.InvariantCulture),
                FlowDesignerControl.BrushFromRgb(100, 116, 139)));

            if (entry.TriggerKind == FlowTriggerKind.Manual)
            {
                RenderManualInputs(entry);
                return;
            }

            _rows.Children.Add(CreateStatusText(
                entry.TriggerKind == FlowTriggerKind.External
                    ? "该入口由外部宿主触发，设计器仅展示入口协议。"
                    : "该入口由监听节点事件触发，设计器仅展示入口协议。",
                FlowDesignerControl.BrushFromRgb(180, 83, 9)));
            RenderReadOnlyInputProtocol(entry.Inputs);
        }

        private void RenderManualInputs(FlowEntryDefinition entry)
        {
            var inputs = entry.Inputs ?? new List<TriggerInputDescriptor>();
            if (inputs.Count == 0)
            {
                _rows.Children.Add(CreateStatusText("该手动入口没有输入参数。", FlowDesignerControl.BrushFromRgb(100, 116, 139)));
                return;
            }

            _rows.Children.Add(CreateSection("手动触发输入"));
            var values = GetEntryValues(entry.EntryName);
            foreach (var input in inputs.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                _rows.Children.Add(CreateLabel(GetInputLabel(input) + (input.IsRequired ? " *" : string.Empty)));
                object value;
                if (!values.TryGetValue(input.Name, out value))
                {
                    value = input.DefaultValue;
                    if (value == null && input.DataType == FlowDataType.Boolean && input.IsRequired)
                    {
                        value = false;
                    }

                    values[input.Name] = value;
                }

                _rows.Children.Add(CreateInputEditor(input, value, values));
                if (!string.IsNullOrWhiteSpace(input.Description))
                {
                    _rows.Children.Add(CreateStatusText(input.Description, FlowDesignerControl.BrushFromRgb(100, 116, 139)));
                }
            }
        }

        private void RenderReadOnlyInputProtocol(IEnumerable<TriggerInputDescriptor> inputs)
        {
            var items = inputs == null ? new List<TriggerInputDescriptor>() : inputs.Where(x => x != null).ToList();
            if (items.Count == 0)
            {
                return;
            }

            _rows.Children.Add(CreateSection("入口输入协议"));
            foreach (var input in items)
            {
                _rows.Children.Add(CreateStatusText(
                    GetInputLabel(input) + " : " + FlowEnumConverter.ToWireValue(input.DataType) +
                    (input.IsRequired ? "（必填）" : "（可选）"),
                    FlowDesignerControl.BrushFromRgb(71, 85, 105)));
            }
        }

        private UIElement CreateInputEditor(
            TriggerInputDescriptor input,
            object value,
            IDictionary<string, object> values)
        {
            if (input.DataType == FlowDataType.Boolean)
            {
                var checkBox = new CheckBox
                {
                    IsThreeState = !input.IsRequired,
                    IsChecked = value == null ? default(bool?) : Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                    IsEnabled = !_isReadOnly,
                    Tag = input.Name,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                checkBox.Checked += delegate { if (!_isReadOnly) values[input.Name] = true; };
                checkBox.Unchecked += delegate { if (!_isReadOnly) values[input.Name] = false; };
                checkBox.Indeterminate += delegate { if (!_isReadOnly) values[input.Name] = null; };
                return checkBox;
            }

            var textBox = new TextBox
            {
                Text = value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture),
                IsReadOnly = _isReadOnly,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225),
                Tag = input.Name
            };
            textBox.TextChanged += delegate
            {
                if (!_isReadOnly)
                {
                    values[input.Name] = textBox.Text;
                }
            };
            return textBox;
        }

        private Dictionary<string, object> GetEntryValues(string entryName)
        {
            var key = entryName ?? string.Empty;
            Dictionary<string, object> values;
            if (!_valuesByEntry.TryGetValue(key, out values))
            {
                values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                _valuesByEntry[key] = values;
            }

            return values;
        }

        private static bool TryConvertValue(
            object value,
            FlowDataType dataType,
            out object converted,
            out string error)
        {
            converted = null;
            error = null;
            var text = value as string;
            try
            {
                switch (dataType)
                {
                    case FlowDataType.String:
                        converted = Convert.ToString(value, CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Int32:
                        converted = value is int ? value : int.Parse(text ?? Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Int64:
                        converted = value is long ? value : long.Parse(text ?? Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Double:
                        converted = value is double ? value : double.Parse(text ?? Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                        return true;
                    case FlowDataType.Boolean:
                        converted = value is bool ? value : bool.Parse(text ?? Convert.ToString(value, CultureInfo.InvariantCulture));
                        return true;
                    case FlowDataType.DateTime:
                        converted = value is DateTime
                            ? value
                            : DateTime.Parse(text ?? Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                        return true;
                    case FlowDataType.Object:
                        converted = value;
                        return true;
                    default:
                        error = "设计器不支持手动输入 " + FlowEnumConverter.ToWireValue(dataType) + " 类型。";
                        return false;
                }
            }
            catch (Exception)
            {
                error = "无法转换为 " + FlowEnumConverter.ToWireValue(dataType) + "。";
                return false;
            }
        }

        private static bool IsEmpty(object value)
        {
            return value == null || (value is string && string.IsNullOrWhiteSpace((string)value));
        }

        private static string GetInputLabel(TriggerInputDescriptor input)
        {
            return string.IsNullOrWhiteSpace(input.DisplayName)
                ? input.Name
                : input.DisplayName + " (" + input.Name + ")";
        }

        private static string GetTriggerKindDisplayName(FlowTriggerKind kind)
        {
            switch (kind)
            {
                case FlowTriggerKind.Manual:
                    return "手动触发";
                case FlowTriggerKind.External:
                    return "外部触发";
                case FlowTriggerKind.NodeEvent:
                    return "节点事件";
                default:
                    return FlowEnumConverter.ToWireValue(kind);
            }
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
                Margin = new Thickness(0, 12, 0, 2)
            };
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 7, 0, 3)
            };
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
