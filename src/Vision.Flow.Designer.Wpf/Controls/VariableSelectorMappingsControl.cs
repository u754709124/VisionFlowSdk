using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Designer.Wpf.Theming;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 编辑有序的“目标字段名 + 结构化变量来源”映射集合。
    /// </summary>
    public sealed class VariableSelectorMappingsControl : UserControl
    {
        private readonly IList<VariableSelectionOption> _options;
        private readonly VariableSelectorScopeFlags _allowedSources;
        private readonly StackPanel _rows;
        private readonly List<VariableSelectorFieldMapping> _mappings;

        /// <summary>创建字段映射编辑器，并仅保留 Descriptor 允许的变量来源。</summary>
        public VariableSelectorMappingsControl(
            IEnumerable<VariableSelectionOption> options,
            VariableSelectorScopeFlags allowedSources)
        {
            _allowedSources = allowedSources;
            _options = (options ?? Enumerable.Empty<VariableSelectionOption>())
                .Where(x => x != null && x.Selector != null && IsSourceAllowed(allowedSources, x.Selector.Scope))
                .ToList();
            _mappings = new List<VariableSelectorFieldMapping>();
            FlowDesignerTheme.ApplyTo(this);

            var layout = new StackPanel();
            var header = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            var nameHeader = new TextBlock { Text = "Attribute 名称", Margin = new Thickness(8, 0, 8, 0) };
            var sourceHeader = new TextBlock { Text = "变量来源", Margin = new Thickness(8, 0, 8, 0) };
            Grid.SetColumn(sourceHeader, 1);
            header.Children.Add(nameHeader);
            header.Children.Add(sourceHeader);
            layout.Children.Add(header);

            _rows = new StackPanel();
            layout.Children.Add(_rows);

            var addButton = new Button
            {
                Content = "新增映射",
                Tag = "VariableMappingAdd",
                MinWidth = 88,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 6, 0, 0)
            };
            addButton.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.SecondaryButtonStyleKey);
            addButton.Click += delegate
            {
                if (IsEnabled)
                {
                    AddMapping();
                }
            };
            layout.Children.Add(addButton);
            Content = layout;
        }

        /// <summary>映射内容或顺序变化后触发。</summary>
        public event Action<IList<VariableSelectorFieldMapping>> MappingsChanged;

        /// <summary>获取映射的防御性副本。</summary>
        public IList<VariableSelectorFieldMapping> Mappings
        {
            get { return _mappings.Select(CloneMapping).ToList(); }
        }

        /// <summary>获取当前映射的校验错误；空值表示校验通过。</summary>
        public string ValidationError
        {
            get
            {
                if (_mappings.Any(x => string.IsNullOrWhiteSpace(x.AttributeName)))
                {
                    return "Attribute 名称不能为空。";
                }

                if (_mappings.Where(x => !string.IsNullOrWhiteSpace(x.AttributeName))
                    .GroupBy(x => x.AttributeName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Any(x => x.Count() > 1))
                {
                    return "Attribute 名称不能重复（不区分大小写）。";
                }

                foreach (var mapping in _mappings)
                {
                    if (mapping.Source == null)
                    {
                        return "每一项映射都必须选择变量来源。";
                    }

                    if (!IsSourceAllowed(_allowedSources, mapping.Source.Scope))
                    {
                        return "映射包含 Descriptor 不允许的变量来源。";
                    }

                    if (!_options.Any(x => x.Matches(mapping.Source)))
                    {
                        return "映射包含当前控制流路径上不可用的变量来源：" +
                            VariableSelectionOption.FormatSelector(mapping.Source) + "。";
                    }
                }

                return null;
            }
        }

        /// <summary>加载流程设置常量中的有序映射。</summary>
        public void ShowMappings(object value)
        {
            _mappings.Clear();
            _mappings.AddRange(VariableSelectorFieldMapping.ReadCollection(value).Select(CloneMapping));
            RenderRows();
        }

        /// <summary>在末尾新增一个空映射并进入待完善状态。</summary>
        public void AddMapping()
        {
            _mappings.Add(new VariableSelectorFieldMapping());
            RenderRows();
            RaiseMappingsChanged();
        }

        /// <summary>删除指定位置的映射。</summary>
        public void RemoveMapping(int index)
        {
            if (index < 0 || index >= _mappings.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }

            _mappings.RemoveAt(index);
            RenderRows();
            RaiseMappingsChanged();
        }

        /// <summary>将指定映射移动到新位置。</summary>
        public void MoveMapping(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= _mappings.Count)
            {
                throw new ArgumentOutOfRangeException("oldIndex");
            }

            if (newIndex < 0 || newIndex >= _mappings.Count)
            {
                throw new ArgumentOutOfRangeException("newIndex");
            }

            if (oldIndex == newIndex)
            {
                return;
            }

            var item = _mappings[oldIndex];
            _mappings.RemoveAt(oldIndex);
            _mappings.Insert(newIndex, item);
            RenderRows();
            RaiseMappingsChanged();
        }

        private void RenderRows()
        {
            _rows.Children.Clear();
            for (var index = 0; index < _mappings.Count; index++)
            {
                _rows.Children.Add(CreateRow(index));
            }
        }

        private UIElement CreateRow(int index)
        {
            var mapping = _mappings[index];
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6), Tag = "VariableMappingRow:" + index };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            var nameEditor = new TextBox
            {
                Text = mapping.AttributeName ?? string.Empty,
                IsReadOnly = !IsEnabled,
                Tag = "VariableMappingName:" + index,
                Margin = new Thickness(0, 0, 6, 0)
            };
            nameEditor.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.FieldTextBoxStyleKey);
            nameEditor.TextChanged += delegate
            {
                if (IsEnabled)
                {
                    mapping.AttributeName = nameEditor.Text;
                    RaiseMappingsChanged();
                }
            };
            row.Children.Add(nameEditor);

            var sourceEditor = new VariableSelectorControl(_options)
            {
                IsEnabled = IsEnabled,
                Tag = "VariableMappingSource:" + index,
                Margin = new Thickness(0, 0, 6, 0)
            };
            sourceEditor.ShowSelector(mapping.Source);
            sourceEditor.VariableSelected += delegate(VariableSelectionOption selected)
            {
                if (IsEnabled && selected != null)
                {
                    mapping.Source = CloneSelector(selected.Selector);
                    RaiseMappingsChanged();
                }
            };
            Grid.SetColumn(sourceEditor, 1);
            row.Children.Add(sourceEditor);

            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(CreateDeleteButton(index));
            Grid.SetColumn(actions, 2);
            row.Children.Add(actions);
            return row;
        }

        private Button CreateDeleteButton(int index)
        {
            var button = new Button
            {
                Content = "×",
                Tag = "VariableMappingDelete:" + index,
                Width = 34,
                Height = 32,
                Margin = new Thickness(0, 0, 3, 0),
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                IsEnabled = IsEnabled
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, FlowDesignerTheme.DangerButtonStyleKey);
            button.Click += delegate { RemoveMapping(index); };
            return button;
        }

        private void RaiseMappingsChanged()
        {
            var handler = MappingsChanged;
            if (handler != null)
            {
                handler(Mappings);
            }
        }

        private static VariableSelectorFieldMapping CloneMapping(VariableSelectorFieldMapping mapping)
        {
            return new VariableSelectorFieldMapping
            {
                AttributeName = mapping == null ? null : mapping.AttributeName,
                Source = mapping == null ? null : CloneSelector(mapping.Source)
            };
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
                case VariableSelectorScope.GlobalVariable:
                    return (flags & VariableSelectorScopeFlags.GlobalVariable) != 0;
                default:
                    return false;
            }
        }
    }
}
