using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core;
using Vision.Flow.Designer.Wpf;

namespace Vision.Flow.Tests
{
    // Designer 控件测试在 STA 线程运行，覆盖调试只读模式和节点运行状态摘要。
    internal static class DesignerInteractionTests
    {
        public static Task PropertyPanelReadOnlyDisablesEditors()
        {
            RunOnSta(delegate
            {
                var node = CreateNode();
                var descriptor = CreateDescriptor();
                var panel = new PropertyPanelControl();

                panel.ShowNode(node, descriptor, new[] { "{{ source.Image }}" }, delegate { }, true);

                var textBoxes = FindChildren<TextBox>(panel).ToList();
                var checkBoxes = FindChildren<CheckBox>(panel).ToList();
                var variableSelectors = FindChildren<VariableSelectorControl>(panel).ToList();

                AssertEx.True(textBoxes.Count >= 3, "Property panel should render text editors.");
                AssertEx.True(textBoxes.All(x => x.IsReadOnly), "Read-only property panel should make every TextBox read-only.");
                AssertEx.True(checkBoxes.Count >= 1 && checkBoxes.All(x => !x.IsEnabled), "Read-only property panel should disable CheckBox editors.");
                AssertEx.True(variableSelectors.Count >= 1 && variableSelectors.All(x => !x.IsEnabled), "Read-only property panel should disable variable selector buttons.");
            });
            return Task.FromResult(0);
        }

        public static Task NodePaletteReadOnlyBlocksNodeRequests()
        {
            RunOnSta(delegate
            {
                var palette = new NodePaletteControl();
                var descriptor = CreateDescriptor();
                var requested = false;
                palette.NodeRequested += delegate { requested = true; };
                palette.SetDescriptors(new[] { descriptor });

                palette.SetReadOnly(true);

                var button = FindChildren<Button>(palette).FirstOrDefault(x => object.ReferenceEquals(x.Tag, descriptor));
                AssertEx.NotNull(button, "Palette should render a button for the descriptor.");
                AssertEx.False(button.IsEnabled, "Read-only palette should disable descriptor buttons.");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
                AssertEx.False(requested, "Read-only palette should not raise NodeRequested.");
            });
            return Task.FromResult(0);
        }

        public static Task NodeCardShowsRuntimeSummaryAboveCard()
        {
            RunOnSta(delegate
            {
                var card = new NodeCardControl(new NodeViewModel(CreateNode(), CreateDescriptor()));

                card.SetRuntimeState(NodeRuntimeState.Completed, TimeSpan.FromMilliseconds(12), null);

                var texts = FindChildren<TextBlock>(card).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("成功", StringComparison.OrdinalIgnoreCase) >= 0 && x.IndexOf("12ms", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Completed node card should show success and elapsed time in the runtime summary.");

                card.SetRuntimeState(NodeRuntimeState.Failed, TimeSpan.FromMilliseconds(34), "Camera timeout detail");
                texts = FindChildren<TextBlock>(card).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0 && x.IndexOf("34ms", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Failed node card should show failure and elapsed time in the runtime summary.");
                AssertEx.True(Convert.ToString(card.ToolTip, CultureInfo.InvariantCulture).IndexOf("Camera timeout detail", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Failed node card should keep the full failure reason in the tooltip.");
            });
            return Task.FromResult(0);
        }

        private static NodeDefinition CreateNode()
        {
            return new NodeDefinition
            {
                Id = "node_1",
                Name = "Test Node",
                Type = "test.node",
                Version = "1.0.0",
                Settings =
                {
                    { "Message", "hello" },
                    { "Enabled", true }
                }
            };
        }

        private static NodeDescriptor CreateDescriptor()
        {
            var descriptor = new NodeDescriptor
            {
                NodeType = "test.node",
                DisplayName = "Test Node",
                Category = "Test",
                Version = "1.0.0"
            };
            descriptor.Settings.Add(new NodeSettingDescriptor
            {
                Name = "Message",
                DisplayName = "Message",
                DataType = "String"
            });
            descriptor.Settings.Add(new NodeSettingDescriptor
            {
                Name = "Enabled",
                DisplayName = "Enabled",
                DataType = "Boolean"
            });
            descriptor.InputPorts.Add(new NodePortDescriptor
            {
                Name = "Image",
                DisplayName = "Image",
                Direction = "Input",
                DataType = "Image"
            });
            descriptor.OutputPorts.Add(new NodePortDescriptor
            {
                Name = FlowPortNames.Next,
                DisplayName = "Next",
                Direction = "Output",
                DataType = "Control"
            });
            return descriptor;
        }

        private static IEnumerable<T> FindChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            var logicalChildren = LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToList();
            foreach (var child in logicalChildren)
            {
                var typed = child as T;
                if (typed != null)
                {
                    yield return typed;
                }

                foreach (var nested in FindChildren<T>(child))
                {
                    yield return nested;
                }
            }

            var visualCount = 0;
            try
            {
                visualCount = VisualTreeHelper.GetChildrenCount(root);
            }
            catch (InvalidOperationException)
            {
                visualCount = 0;
            }

            for (var index = 0; index < visualCount; index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (logicalChildren.Contains(child))
                {
                    continue;
                }

                var typed = child as T;
                if (typed != null)
                {
                    yield return typed;
                }

                foreach (var nested in FindChildren<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static void RunOnSta(Action action)
        {
            Exception error = null;
            var thread = new Thread(delegate()
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (error != null)
            {
                throw new InvalidOperationException("STA designer test failed.", error);
            }
        }
    }
}
