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
using Vision.Flow.Nodes;
using ShapesPath = System.Windows.Shapes.Path;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 灞炴€ч潰鏉挎帶浠惰礋璐ｇ紪杈戣妭鐐硅缃拰鍙橀噺缁戝畾銆?
    public sealed class PropertyPanelControl : Border
    {
        private readonly StackPanel _rows;
        private NodeDefinition _node;
        private Action _changed;
        private IList<string> _variableExpressions;
        private bool _isReadOnly;

        public PropertyPanelControl()
        {
            Padding = new Thickness(12);
            Background = Brushes.White;
            BorderBrush = FlowDesignerControl.BrushFromRgb(222, 229, 238);
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(6);

            _rows = new StackPanel();
            _variableExpressions = new List<string>();
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _rows
            };
        }

        public void ShowNode(NodeDefinition node, NodeDescriptor descriptor, Action changed)
        {
            ShowNode(node, descriptor, null, changed, false);
        }

        public void ShowNode(NodeDefinition node, NodeDescriptor descriptor, IEnumerable<string> variableExpressions, Action changed)
        {
            ShowNode(node, descriptor, variableExpressions, changed, false);
        }

        public void ShowNode(NodeDefinition node, NodeDescriptor descriptor, IEnumerable<string> variableExpressions, Action changed, bool isReadOnly)
        {
            _node = node;
            _changed = changed;
            _isReadOnly = isReadOnly;
            _variableExpressions = variableExpressions == null
                ? new List<string>()
                : variableExpressions
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            _rows.Children.Clear();

            _rows.Children.Add(CreateTitle("Properties"));
            if (node == null)
            {
                _rows.Children.Add(CreateMutedText("Select a node on the canvas."));
                return;
            }

            AddTextField("Id", node.Id, false, null);
            AddTextField("Name", node.Name, true, delegate(string text)
            {
                node.Name = text;
            });
            AddTextField("Type", node.Type, false, null);

            _rows.Children.Add(CreateSection("Settings"));
            if (descriptor != null)
            {
                foreach (var setting in descriptor.Settings)
                {
                    object value;
                    node.Settings.TryGetValue(setting.Name, out value);
                    AddSettingField(setting, value, delegate(object newValue)
                    {
                        node.Settings[setting.Name] = newValue;
                    });
                }
            }

            if (descriptor != null && descriptor.InputPorts.Count > 0)
            {
                _rows.Children.Add(CreateSection("Input Bindings"));
                foreach (var input in descriptor.InputPorts)
                {
                    VariableBinding binding;
                    var text = node.InputBindings.TryGetValue(input.Name, out binding) && binding != null
                        ? binding.Expression
                        : string.Empty;
                    AddBindingField(input.DisplayName + " (" + input.Name + ")", text, delegate(string value)
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            node.InputBindings.Remove(input.Name);
                        }
                        else
                        {
                            node.InputBindings[input.Name] = VariableBinding.ForExpression(value);
                        }
                    });
                }
            }

            if (descriptor != null && descriptor.Outputs.Count > 0)
            {
                _rows.Children.Add(CreateSection("Outputs"));
                foreach (var output in descriptor.Outputs)
                {
                    _rows.Children.Add(CreateMutedText(output.Name + " : " + FlowEnumConverter.ToWireValue(output.DataType)));
                }
            }
        }

        private void AddSettingField(NodeSettingDescriptor setting, object value, Action<object> setter)
        {
            var label = setting.DisplayName + " (" + setting.Name + ")";
            if (setting.DataType == FlowDataType.Boolean)
            {
                _rows.Children.Add(CreateLabel(label));
                var checkBox = new CheckBox
                {
                    IsChecked = value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                    IsEnabled = !_isReadOnly,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                checkBox.Checked += delegate
                {
                    if (!_isReadOnly)
                    {
                        ApplySetting(setter, true);
                    }
                };
                checkBox.Unchecked += delegate
                {
                    if (!_isReadOnly)
                    {
                        ApplySetting(setter, false);
                    }
                };
                _rows.Children.Add(checkBox);
                return;
            }

            if (IsBindingSetting(setting))
            {
                AddBindingField(label, ToEditorText(setting, value), delegate(string text)
                {
                    ApplySetting(setter, ConvertFromEditorText(setting, text));
                });
                return;
            }

            var selectorItems = GetSelectorItems(setting);
            if (selectorItems.Count > 0)
            {
                _rows.Children.Add(CreateLabel(label));
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

                comboBox.LostFocus += delegate { ApplySetting(setter, ConvertFromEditorText(setting, comboBox.Text)); };
                comboBox.DropDownClosed += delegate { ApplySetting(setter, ConvertFromEditorText(setting, comboBox.Text)); };
                _rows.Children.Add(comboBox);
                return;
            }

            AddTextField(label, ToEditorText(setting, value), true, delegate(string text)
            {
                ApplySetting(setter, ConvertFromEditorText(setting, text));
            });
        }

        private void AddBindingField(string label, string value, Action<string> setter)
        {
            _rows.Children.Add(CreateLabel(label));
            var dock = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 4)
            };
            var selector = new VariableSelectorControl(_variableExpressions);
            selector.IsEnabled = !_isReadOnly;
            DockPanel.SetDock(selector, Dock.Right);
            dock.Children.Add(selector);

            var textBox = new TextBox
            {
                Text = value ?? string.Empty,
                IsReadOnly = _isReadOnly,
                MinHeight = 28,
                Padding = new Thickness(7, 4, 7, 4),
                BorderBrush = FlowDesignerControl.BrushFromRgb(203, 213, 225)
            };
            selector.VariableSelected += delegate(string expression)
            {
                if (_isReadOnly)
                {
                    return;
                }

                textBox.Text = string.IsNullOrWhiteSpace(textBox.Text) ? expression : textBox.Text + " " + expression;
                setter(textBox.Text);
                RaiseChanged();
            };
            textBox.LostFocus += delegate
            {
                if (_isReadOnly)
                {
                    return;
                }

                setter(textBox.Text);
                RaiseChanged();
            };
            dock.Children.Add(textBox);
            _rows.Children.Add(dock);
        }

        private static bool IsBindingSetting(NodeSettingDescriptor setting)
        {
            if (setting == null || string.IsNullOrWhiteSpace(setting.Name))
            {
                return false;
            }

            if (string.Equals(setting.Name, "FieldMappings", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return setting.Name.EndsWith("Binding", StringComparison.OrdinalIgnoreCase) ||
                setting.Name.EndsWith("Bindings", StringComparison.OrdinalIgnoreCase);
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
                AcceptsReturn = label.IndexOf("Mappings", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("Channels", StringComparison.OrdinalIgnoreCase) >= 0,
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

        private static TextBlock CreateLabel(string label)
        {
            return new TextBlock
            {
                Text = label,
                Foreground = FlowDesignerControl.BrushFromRgb(71, 85, 105),
                Margin = new Thickness(0, 8, 0, 3)
            };
        }

        private void ApplySetting(Action<object> setter, object value)
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
            return new TextBlock
            {
                Text = text,
                Foreground = FlowDesignerControl.BrushFromRgb(100, 116, 139),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 3)
            };
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
                return ToPairText(value, "FieldName", "ValueBinding", "Value");
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

            return string.IsNullOrWhiteSpace(text) && setting.IsRequired == false ? null : text;
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
                var mapping = new Dictionary<string, object>
                {
                    { "FieldName", pair.Key }
                };
                if (pair.Value != null && pair.Value.Trim().StartsWith("{{", StringComparison.Ordinal))
                {
                    mapping["ValueBinding"] = pair.Value;
                }
                else
                {
                    mapping["Value"] = pair.Value;
                }

                result.Add(mapping);
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
            return ToPairText(value, keyName, valueName, null);
        }

        private static string ToPairText(object value, string keyName, string valueName, string fallbackValueName)
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
                if (pairValue == null && fallbackValueName != null)
                {
                    pairValue = GetDictionaryValue(dictionary, fallbackValueName);
                }

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
