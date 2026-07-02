using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Vision.Flow.Designer.Wpf.Controls
{
    // 变量选择控件负责把设计器可见变量表达式插入到绑定输入框。
    public sealed class VariableSelectorControl : Button
    {
        private readonly IList<string> _variables;

        public VariableSelectorControl()
            : this(null)
        {
        }

        public VariableSelectorControl(IEnumerable<string> variables)
        {
            _variables = variables == null
                ? new List<string>()
                : variables
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            Content = "Var";
            MinWidth = 44;
            Height = 28;
            Margin = new Thickness(6, 0, 0, 0);
            ToolTip = "Insert a variable binding.";
            Click += OnClick;
        }

        public event Action<string> VariableSelected;

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (_variables.Count == 0)
            {
                RaiseVariableSelected("{{ node.Output }}");
                return;
            }

            var menu = new ContextMenu
            {
                PlacementTarget = this
            };
            foreach (var variable in _variables)
            {
                var item = new MenuItem
                {
                    Header = variable
                };
                item.Click += delegate { RaiseVariableSelected(variable); };
                menu.Items.Add(item);
            }

            ContextMenu = menu;
            menu.IsOpen = true;
        }

        private void RaiseVariableSelected(string expression)
        {
            var handler = VariableSelected;
            if (handler != null)
            {
                handler(expression);
            }
        }
    }
}
