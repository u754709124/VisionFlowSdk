using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

namespace Vision.Flow.Tests
{
    // Designer 鎺т欢娴嬭瘯鍦?STA 绾跨▼杩愯锛岃鐩栬皟璇曞彧璇绘ā寮忓拰鑺傜偣杩愯鐘舵€佹憳瑕併€?
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
                RaiseDoubleClick(button);
                AssertEx.False(requested, "Read-only palette should not raise NodeRequested.");
                AssertEx.False(palette.RequestNodeDrag(descriptor, button), "Read-only palette should not start node drag requests.");
            });
            return Task.FromResult(0);
        }

        public static Task NodePaletteSingleClickSelectsOnly()
        {
            RunOnSta(delegate
            {
                var palette = new NodePaletteControl();
                var descriptor = CreateDescriptor();
                var requested = 0;
                palette.NodeRequested += delegate { requested++; };
                palette.SetDescriptors(new[] { descriptor });

                var button = FindPaletteButton(palette, descriptor);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

                AssertEx.Equal(0, requested, "Single-clicking a palette item should not add a node.");
                AssertEx.True(object.ReferenceEquals(descriptor, palette.SelectedDescriptor), "Single-clicking a palette item should select that descriptor.");
            });
            return Task.FromResult(0);
        }

        public static Task NodePaletteDoubleClickRequestsNodeOnce()
        {
            RunOnSta(delegate
            {
                var palette = new NodePaletteControl();
                var descriptor = CreateDescriptor();
                var requested = 0;
                NodeDescriptor requestedDescriptor = null;
                palette.NodeRequested += delegate(NodeDescriptor item)
                {
                    requested++;
                    requestedDescriptor = item;
                };
                palette.SetDescriptors(new[] { descriptor });

                RaiseDoubleClick(FindPaletteButton(palette, descriptor));

                AssertEx.Equal(1, requested, "Double-clicking a palette item should request one node.");
                AssertEx.True(object.ReferenceEquals(descriptor, requestedDescriptor), "Double-click node request should carry the clicked descriptor.");
            });
            return Task.FromResult(0);
        }

        public static Task NodePaletteDragRequestCarriesDescriptor()
        {
            RunOnSta(delegate
            {
                var palette = new NodePaletteControl();
                var descriptor = CreateDescriptor();
                var requested = 0;
                NodePaletteDragEventArgs args = null;
                palette.NodeDragRequested += delegate(object sender, NodePaletteDragEventArgs e)
                {
                    requested++;
                    args = e;
                };
                palette.SetDescriptors(new[] { descriptor });

                var button = FindPaletteButton(palette, descriptor);
                AssertEx.True(palette.RequestNodeDrag(descriptor, button), "Editable palette should start node drag requests.");

                AssertEx.Equal(1, requested, "Editable palette drag should raise one drag request.");
                AssertEx.NotNull(args, "Palette drag request should include event args.");
                AssertEx.True(object.ReferenceEquals(descriptor, args.Descriptor), "Palette drag request should carry the descriptor.");
                AssertEx.True(object.ReferenceEquals(button, args.DragSource), "Palette drag request should carry the drag source.");
            });
            return Task.FromResult(0);
        }

        public static Task StopMarksRunningCardsStopped()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, null, new FlowDesignerOptions { LoadSampleOnStartup = false });
                var node = CreateNode();
                var card = new NodeCardControl(new NodeViewModel(node, CreateDescriptor()));
                card.SetRuntimeState(NodeRuntimeState.Running, null, null);

                GetPrivateField<Dictionary<string, NodeCardControl>>(control, "_nodeCards")[node.Id] = card;
                GetPrivateField<Dictionary<string, DateTime>>(control, "_nodeStartTimes")[node.Id] = DateTime.UtcNow.AddMilliseconds(-42);

                InvokePrivate(control, "MarkRunningNodeStatesStopped");

                var texts = FindChildren<TextBlock>(card).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("已停止", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Stopping debug should move running cards out of Running and show a stopped state.");
                AssertEx.False(texts.Any(x => x.IndexOf("运行中", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Stopped node card should not keep showing Running.");
            });
            return Task.FromResult(0);
        }

        public static Task DebugButtonsRecoverAfterStop()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, null, new FlowDesignerOptions { LoadSampleOnStartup = false });
                SetDesignerMode(control, "DebugRun");

                SetPrivateField(control, "_isDebugRunning", true);
                InvokePrivate(control, "UpdateInteractionModeUi");
                AssertEx.False(GetPrivateField<Button>(control, "_debugRunButton").IsEnabled, "Debug Run should be disabled while a debug run is active.");
                AssertEx.True(GetPrivateField<Button>(control, "_stopButton").IsEnabled, "Stop should be enabled while a debug run is active.");

                SetPrivateField(control, "_isDebugRunning", false);
                InvokePrivate(control, "UpdateInteractionModeUi");
                AssertEx.True(GetPrivateField<Button>(control, "_debugRunButton").IsEnabled, "Debug Run should be re-enabled after Stop.");
                AssertEx.False(GetPrivateField<Button>(control, "_stopButton").IsEnabled, "Stop should be disabled after Stop finishes.");
            });
            return Task.FromResult(0);
        }

        public static Task EmbeddedToolbarHidesStandaloneDocumentCommands()
        {
            RunOnSta(delegate
            {
                var defaultOptions = new FlowDesignerOptions { LoadSampleOnStartup = false };
                AssertEx.True(defaultOptions.ShowStandaloneDocumentCommands,
                    "Standalone document commands should remain enabled by default for compatibility.");

                var standalone = new FlowDesignerControl(null, null, defaultOptions);
                var standaloneLabels = FindChildren<Button>(standalone)
                    .Select(x => x.Content as string)
                    .Where(x => x != null)
                    .ToList();
                AssertEx.True(new[] { "New", "Sample", "Open", "Save", "Publish" }.All(standaloneLabels.Contains),
                    "Default designer toolbar should keep all standalone document commands.");

                var embedded = new FlowDesignerControl(null, null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                var embeddedLabels = FindChildren<Button>(embedded)
                    .Select(x => x.Content as string)
                    .Where(x => x != null)
                    .ToList();
                AssertEx.False(new[] { "New", "Sample", "Open", "Save", "Publish" }.Any(embeddedLabels.Contains),
                    "Embedded designer toolbar should hide standalone document commands.");
                AssertEx.True(new[] { "编辑", "调试运行", "Debug Run", "Stop" }.All(embeddedLabels.Contains),
                    "Embedded designer toolbar should keep mode, run and stop commands with readable labels.");
            });
            return Task.FromResult(0);
        }

        public static Task HostDocumentApiLoadsCapturesAndDeepCopies()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                var source = CreateHostDocument();
                SetDesignerMode(control, "DebugRun");

                control.LoadDocumentAsync(source).GetAwaiter().GetResult();
                AssertEx.Equal("Edit", GetPrivateField<object>(control, "_interactionMode").ToString(),
                    "Host load should switch the designer back to Edit mode.");

                source.Runtime.Nodes[0].Name = "Changed outside designer";
                var cards = GetPrivateField<Dictionary<string, NodeCardControl>>(control, "_nodeCards");
                var card = cards["node_1"];
                Canvas.SetLeft(card, 416.0);
                Canvas.SetTop(card, 288.0);

                var scale = GetPrivateField<ScaleTransform>(control, "_canvasScale");
                scale.ScaleX = 1.35;
                scale.ScaleY = 1.35;

                control.Measure(new Size(1120, 720));
                control.Arrange(new Rect(0, 0, 1120, 720));
                control.UpdateLayout();
                var scroll = GetPrivateField<ScrollViewer>(control, "_canvasScroll");
                scroll.ScrollToHorizontalOffset(144.0);
                scroll.ScrollToVerticalOffset(96.0);
                control.UpdateLayout();
                AssertEx.True(scroll.HorizontalOffset > 0 && scroll.VerticalOffset > 0,
                    "Designer test layout should expose scrollable canvas offsets.");

                var captured = control.CaptureDocument();
                AssertEx.False(object.ReferenceEquals(source, captured), "Capture should return a separate document instance.");
                AssertEx.Equal("Host Node", captured.Runtime.Nodes[0].Name,
                    "Loading should isolate the designer from later source document changes.");
                AssertEx.Equal(416.0, captured.View.Nodes["node_1"].X,
                    "Capture should synchronize the rendered node X coordinate.");
                AssertEx.Equal(288.0, captured.View.Nodes["node_1"].Y,
                    "Capture should synchronize the rendered node Y coordinate.");
                AssertEx.Equal(1.35, captured.View.Zoom, "Capture should synchronize the current canvas zoom.");
                AssertEx.Equal(scroll.HorizontalOffset, captured.View.OffsetX,
                    "Capture should synchronize the current horizontal offset.");
                AssertEx.Equal(scroll.VerticalOffset, captured.View.OffsetY,
                    "Capture should synchronize the current vertical offset.");

                captured.Runtime.Nodes[0].Name = "Changed captured copy";
                captured.View.Nodes["node_1"].X = 999;
                var capturedAgain = control.CaptureDocument();
                AssertEx.Equal("Host Node", capturedAgain.Runtime.Nodes[0].Name,
                    "Changing a captured document should not mutate the designer document.");
                AssertEx.Equal(416.0, capturedAgain.View.Nodes["node_1"].X,
                    "Changing captured view state should not mutate the designer document.");
            });
            return Task.FromResult(0);
        }

        public static Task HostResetCreatesEmptyDocument()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, null, new FlowDesignerOptions { LoadSampleOnStartup = true });

                control.ResetDocumentAsync("strategy-123", "策略连线图").GetAwaiter().GetResult();
                var captured = control.CaptureDocument();

                AssertEx.Equal("strategy-123", captured.FlowId, "Reset should preserve the requested design FlowId.");
                AssertEx.Equal("strategy-123", captured.Runtime.FlowId, "Reset should keep runtime and design FlowId aligned.");
                AssertEx.Equal("策略连线图", captured.FlowName, "Reset should preserve the requested design FlowName.");
                AssertEx.Equal("策略连线图", captured.Runtime.FlowName, "Reset should keep runtime and design FlowName aligned.");
                AssertEx.Equal(0, captured.Runtime.Nodes.Count, "Reset should not add sample nodes.");
                AssertEx.Equal(0, captured.Runtime.Edges.Count, "Reset should create no edges.");
                AssertEx.Equal(0, captured.Runtime.Entries.Count, "Reset should create no entries.");
            });
            return Task.FromResult(0);
        }

        public static Task PaletteDefaultAddUsesViewportCenter()
        {
            var position = InvokePrivateStatic<Point>(
                typeof(FlowDesignerControl),
                "CalculateViewportCenteredNodePosition",
                320.0,
                160.0,
                960.0,
                640.0,
                2.0,
                220.0,
                182.0);

            AssertEx.Equal(290.0, position.X, "Default palette add should center the new node in the visible canvas area.");
            AssertEx.Equal(149.0, position.Y, "Default palette add should center the new node in the visible canvas area.");
            return Task.FromResult(0);
        }

        public static Task NodeCardUsesSharpTextRenderingOptions()
        {
            RunOnSta(delegate
            {
                var card = new NodeCardControl(new NodeViewModel(CreateNode(), CreateDescriptor()));

                AssertEx.True(card.UseLayoutRounding, "Node cards should round layout pixels to reduce blurry text while zoomed out.");
                AssertEx.True(card.SnapsToDevicePixels, "Node cards should snap to device pixels while zoomed out.");
                AssertEx.Equal(TextFormattingMode.Display, TextOptions.GetTextFormattingMode(card), "Node cards should use display text formatting.");
                AssertEx.Equal(TextRenderingMode.ClearType, TextOptions.GetTextRenderingMode(card), "Node cards should use ClearType text rendering.");
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
                var summaryText = FindChildren<TextBlock>(card).FirstOrDefault(x => (x.Text ?? string.Empty).IndexOf("成功", StringComparison.OrdinalIgnoreCase) >= 0);
                AssertRuntimeSummaryIsTextOnly(summaryText);
                AssertEx.True((summaryText.Text ?? string.Empty).IndexOf(" · ", StringComparison.Ordinal) >= 0,
                    "Runtime summary should use a readable middle-dot separator.");
                AssertEx.False((summaryText.Text ?? string.Empty).IndexOf(" 路 ", StringComparison.Ordinal) >= 0,
                    "Runtime summary should not contain the corrupted separator text.");

                card.SetRuntimeState(NodeRuntimeState.Failed, TimeSpan.FromMilliseconds(34), "Camera timeout detail");
                texts = FindChildren<TextBlock>(card).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0 && x.IndexOf("34ms", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Failed node card should show failure and elapsed time in the runtime summary.");
                AssertEx.True(Convert.ToString(card.ToolTip, CultureInfo.InvariantCulture).IndexOf("Camera timeout detail", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Failed node card should keep the full failure reason in the tooltip.");
            });
            return Task.FromResult(0);
        }

        private static void RaiseDoubleClick(Button button)
        {
            AssertEx.NotNull(button, "Palette button should be available before raising double-click.");
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Control.MouseDoubleClickEvent,
                Source = button
            });
        }

        private static Button FindPaletteButton(NodePaletteControl palette, NodeDescriptor descriptor)
        {
            var button = FindChildren<Button>(palette).FirstOrDefault(x => object.ReferenceEquals(x.Tag, descriptor));
            AssertEx.NotNull(button, "Palette should render a button for the descriptor.");
            return button;
        }

        private static void SetDesignerMode(FlowDesignerControl control, string modeName)
        {
            var modeType = typeof(FlowDesignerControl).Assembly.GetType("Vision.Flow.Designer.Wpf.Controls.DesignerInteractionMode");
            AssertEx.NotNull(modeType, "Designer interaction mode type should exist.");
            SetPrivateField(control, "_interactionMode", Enum.Parse(modeType, modeName));
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            AssertEx.NotNull(field, "Private field should exist: " + name);
            return (T)field.GetValue(instance);
        }

        private static void SetPrivateField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            AssertEx.NotNull(field, "Private field should exist: " + name);
            field.SetValue(instance, value);
        }

        private static void InvokePrivate(object instance, string name)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            AssertEx.NotNull(method, "Private method should exist: " + name);
            method.Invoke(instance, new object[0]);
        }

        private static T InvokePrivateStatic<T>(Type type, string name, params object[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            AssertEx.NotNull(method, "Private static method should exist: " + name);
            return (T)method.Invoke(null, args);
        }

        private static void AssertRuntimeSummaryIsTextOnly(TextBlock summaryText)
        {
            AssertEx.NotNull(summaryText, "Runtime summary text should be rendered.");
            var parentBorder = FindAncestor<Border>(summaryText);
            if (parentBorder == null)
            {
                return;
            }

            var hasVisibleBackground = parentBorder.Background != null && parentBorder.Background != Brushes.Transparent;
            var hasVisibleBorder = parentBorder.BorderThickness.Left > 0 ||
                parentBorder.BorderThickness.Top > 0 ||
                parentBorder.BorderThickness.Right > 0 ||
                parentBorder.BorderThickness.Bottom > 0;
            AssertEx.False(hasVisibleBackground || hasVisibleBorder, "Runtime summary should be plain text, not a visible mini-card.");
        }

        private static FlowDesignDocument CreateHostDocument()
        {
            var node = CreateNode();
            node.Name = "Host Node";
            var document = new FlowDesignDocument
            {
                FlowId = "host-flow",
                FlowName = "Host Flow",
                Runtime = new RuntimeFlowDefinition
                {
                    FlowId = "host-flow",
                    FlowName = "Host Flow",
                    Version = "1.0.0"
                },
                View = new FlowViewState
                {
                    Zoom = 1.0,
                    CanvasWidth = 2400,
                    CanvasHeight = 1600
                }
            };
            document.Runtime.Nodes.Add(node);
            document.Runtime.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = node.Id });
            document.View.Nodes[node.Id] = new NodeViewState { X = 80, Y = 96 };
            return document;
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
                DataType = FlowDataType.String
            });
            descriptor.Settings.Add(new NodeSettingDescriptor
            {
                Name = "Enabled",
                DisplayName = "Enabled",
                DataType = FlowDataType.Boolean
            });
            descriptor.InputPorts.Add(new NodePortDescriptor
            {
                Name = "Image",
                DisplayName = "Image",
                Direction = FlowPortDirection.Input,
                DataType = FlowDataType.Object
            });
            descriptor.OutputPorts.Add(new NodePortDescriptor
            {
                Name = FlowPortNames.Next,
                DisplayName = "Next",
                Direction = FlowPortDirection.Output,
                DataType = FlowDataType.Control
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

        private static T FindAncestor<T>(DependencyObject child)
            where T : DependencyObject
        {
            var current = child;
            while (current != null)
            {
                current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current);
                var typed = current as T;
                if (typed != null)
                {
                    return typed;
                }
            }

            return null;
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
