using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Designer.Wpf.Controls
{
    /// <summary>
    /// 选择单个结构化变量来源，不再拼接表达式文本。
    /// </summary>
    public sealed class VariableSelectorControl : Button
    {
        private readonly IList<VariableSelectionOption> _variables;

        /// <summary>
        /// 创建没有候选项的变量选择按钮。
        /// </summary>
        public VariableSelectorControl()
            : this(null)
        {
        }

        /// <summary>
        /// 使用可用候选创建结构化变量选择按钮。
        /// </summary>
        public VariableSelectorControl(IEnumerable<VariableSelectionOption> variables)
        {
            _variables = variables == null
                ? new List<VariableSelectionOption>()
                : variables
                    .Where(x => x != null && x.Selector != null)
                    .GroupBy(x => VariableSelectionOption.FormatSelector(x.Selector), StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList();
            Content = "选择变量";
            MinWidth = 96;
            Height = 28;
            ToolTip = "选择一个可用的前置节点输出或 Token 字段。";
            Click += OnClick;
        }

        /// <summary>
        /// 用户从菜单选择变量后触发。
        /// </summary>
        public event Action<VariableSelectionOption> VariableSelected;

        /// <summary>
        /// 显示当前 Selector；来源失效时仍保留并展示原路径。
        /// </summary>
        public void ShowSelector(VariableSelector selector)
        {
            var option = _variables.FirstOrDefault(x => x.Matches(selector));
            Content = option == null ? VariableSelectionOption.FormatSelector(selector) : option.ShortDisplayText;
            ToolTip = option == null
                ? "当前变量来源不可用：" + VariableSelectionOption.FormatSelector(selector)
                : option.DisplayText;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu
            {
                PlacementTarget = this
            };
            if (_variables.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "没有兼容的可用变量", IsEnabled = false });
                ContextMenu = menu;
                menu.IsOpen = true;
                return;
            }

            foreach (var group in _variables.GroupBy(x => x.GroupName).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var groupItem = new MenuItem
                {
                    Header = group.Key
                };
                foreach (var variable in group.OrderBy(x => x.ValueName, StringComparer.OrdinalIgnoreCase))
                {
                    var item = new MenuItem
                    {
                        Header = variable.DisplayText,
                        ToolTip = VariableSelectionOption.FormatSelector(variable.Selector),
                        Tag = variable
                    };
                    item.Click += delegate(object itemSender, RoutedEventArgs args)
                    {
                        var selected = ((MenuItem)itemSender).Tag as VariableSelectionOption;
                        if (selected != null)
                        {
                            ShowSelector(selected.Selector);
                            RaiseVariableSelected(selected);
                        }
                    };
                    groupItem.Items.Add(item);
                }

                menu.Items.Add(groupItem);
            }

            ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void RaiseVariableSelected(VariableSelectionOption variable)
        {
            var handler = VariableSelected;
            if (handler != null)
            {
                handler(variable);
            }
        }
    }
}
