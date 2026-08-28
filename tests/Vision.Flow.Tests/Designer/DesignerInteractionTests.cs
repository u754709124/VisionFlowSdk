using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
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
using Vision.Flow.Designer.Wpf.Theming;
using Vision.Flow.Designer.Wpf.ViewModels;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // Designer 控件测试在 STA 线程运行，覆盖编辑、布局、文档和属性草稿交互。
    internal static class DesignerInteractionTests
    {
        public static Task TypedObjectMenuSelectsRootAndFirstLayerMembers()
        {
            RunOnSta(delegate
            {
                var rootSelector = VariableSelector.ForNodeOutput("source", "Payload");
                var rootOption = new VariableSelectionOption(
                    rootSelector,
                    "Source [source] / Payload",
                    "Source [source]",
                    "source",
                    "Payload",
                    FlowDataType.Object,
                    null,
                    typeof(DesignerTypedObjectPayload));
                var options = new List<VariableSelectionOption> { rootOption };
                var addMembers = typeof(FlowDesignerControl).GetMethod(
                    "AddFirstLayerMemberSuggestions",
                    BindingFlags.NonPublic | BindingFlags.Static);
                AssertEx.NotNull(addMembers, "The designer should expose its first-layer member suggestion builder internally.");
                addMembers.Invoke(null, new object[] { options, rootOption, typeof(DesignerTypedObjectPayload) });

                AssertEx.True(options.Any(x => x.Selector.Path.SequenceEqual(new[] { "source", "Payload", "Name" })),
                    "A readable public property should become a first-layer variable option.");
                AssertEx.True(options.Any(x => x.Selector.Path.SequenceEqual(new[] { "source", "Payload", "Count" })),
                    "A public field should become a first-layer variable option.");
                AssertEx.True(options.Any(x => x.Selector.Path.SequenceEqual(new[] { "source", "Payload", "Nested" })),
                    "An object-valued first-layer property should remain selectable.");
                AssertEx.False(options.Any(x => x.Selector.Path.Count > 3),
                    "The designer must not recursively expose members below the first layer.");

                var selected = new List<VariableSelectionOption>();
                var selector = new VariableSelectorControl(options, delegate { return true; });
                selector.VariableSelected += delegate(VariableSelectionOption option) { selected.Add(option); };
                selector.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, selector));
                var sourceGroup = selector.ContextMenu.Items.OfType<MenuItem>().Single();
                var rootItem = sourceGroup.Items.OfType<MenuItem>().Single();
                AssertEx.Equal(3, rootItem.Items.Count,
                    "Hovering the typed root item should expose all first-layer members as submenu items.");

                rootItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, rootItem));
                AssertEx.Equal(1, selected.Count, "The typed root menu item should itself remain selectable.");
                AssertEx.Equal(2, selected[0].Selector.Path.Count, "Selecting the root should preserve the root selector path.");

                var nameItem = rootItem.Items.OfType<MenuItem>()
                    .Single(x => Convert.ToString(x.Header, CultureInfo.InvariantCulture).IndexOf("Name", StringComparison.Ordinal) >= 0);
                nameItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, nameItem));
                AssertEx.Equal(2, selected.Count, "A first-layer submenu item should be selectable.");
                AssertEx.Equal("Name", selected[1].Selector.Path[2], "The selected submenu should retain its member path.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyPanelAppliesCustomConstantValidation()
        {
            RunOnSta(delegate
            {
                var node = CreateNode();
                var descriptor = CreateDescriptor();
                descriptor.Settings.First(x => string.Equals(x.Name, "Message", StringComparison.Ordinal))
                    .Validator = delegate(object value)
                    {
                        var text = value as string;
                        return text != null && text.Length >= 3 ? null : "消息至少需要三个字符。";
                    };
                var panel = new PropertyPanelControl();
                panel.ShowNode(
                    node,
                    descriptor,
                    new VariableSelectionOption[0],
                    delegate { },
                    false);

                var editor = FindChildren<TextBox>(panel).FirstOrDefault(
                    x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:Message",
                        StringComparison.Ordinal));
                AssertEx.NotNull(editor, "The validated setting should render a text editor.");

                editor.Text = "x";
                AssertEx.True(panel.HasEditorErrors, "A custom validator error should block applying the property draft.");
                AssertEx.Equal("hello", Convert.ToString(node.Settings["Message"].ConstantValue, CultureInfo.InvariantCulture),
                    "An invalid editor value should not replace the last valid typed constant.");

                editor.Text = "valid";
                AssertEx.False(panel.HasEditorErrors, "Correcting the constant should clear the custom validation error.");
                AssertEx.Equal("valid", Convert.ToString(node.Settings["Message"].ConstantValue, CultureInfo.InvariantCulture),
                    "A valid constant should be written to the property draft.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyPanelReadOnlyDisablesEditors()
        {
            RunOnSta(delegate
            {
                var node = CreateNode();
                var descriptor = CreateDescriptor();
                var panel = new PropertyPanelControl();
                node.Settings["Message"] = NodeSettingValue.ForVariable(
                    VariableSelector.ForNodeOutput("missing", "Image"),
                    "hello");

                panel.ShowNode(node, descriptor, new[]
                {
                    new VariableSelectionOption(
                        VariableSelector.ForNodeOutput("source", "Image"),
                        "Source [source]",
                        "Source",
                        "source",
                        "Image",
                        FlowDataType.String)
                }, delegate { }, true);

                var textBoxes = FindChildren<TextBox>(panel).ToList();
                var checkBoxes = FindChildren<CheckBox>(panel).ToList();
                var variableSelectors = FindChildren<VariableSelectorControl>(panel).ToList();

                AssertEx.True(textBoxes.Count >= 3, "Property panel should render text editors.");
                AssertEx.True(textBoxes.All(x => x.IsReadOnly), "Read-only property panel should make every TextBox read-only.");
                AssertEx.True(checkBoxes.Count >= 1 && checkBoxes.All(x => !x.IsEnabled), "Read-only property panel should disable CheckBox editors.");
                AssertEx.True(variableSelectors.Count >= 1 && variableSelectors.All(x => !x.IsEnabled), "Read-only property panel should disable variable selector buttons.");
                AssertEx.False(FindChildren<TextBlock>(panel).Any(x => string.Equals(x.Text, "Input Bindings", StringComparison.Ordinal)),
                    "Control input ports should not create an Input Bindings section.");
                AssertEx.True(FindChildren<TextBlock>(panel).Any(x => (x.Text ?? string.Empty).IndexOf("变量来源不可用", StringComparison.Ordinal) >= 0),
                    "An unavailable selector should remain visible as an error instead of being deleted.");
                AssertEx.Equal(NodeSettingValueMode.Variable, node.Settings["Message"].Mode,
                    "Rendering an invalid selector in read-only mode should preserve its variable mode.");
                AssertEx.Equal("missing", node.Settings["Message"].Selector.Path[0],
                    "Rendering an invalid selector should preserve its original source path.");

                var editableNode = CreateNode();
                var editablePanel = new PropertyPanelControl();
                var compatibleOption = new VariableSelectionOption(
                    VariableSelector.ForNodeOutput("source", "Image"),
                    "Source [source]",
                    "Source",
                    "source",
                    "Image",
                    FlowDataType.String);
                editablePanel.ShowNode(editableNode, descriptor, new[] { compatibleOption }, delegate { }, false);

                var modeSelector = FindChildren<ComboBox>(editablePanel)
                    .FirstOrDefault(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "Message:Mode", StringComparison.Ordinal));
                AssertEx.NotNull(modeSelector, "A bindable setting should render an inline constant/variable mode selector.");
                var inlineGrid = FindAncestor<Grid>(modeSelector);
                AssertEx.NotNull(inlineGrid, "The mode selector and value editor should share an inline grid.");
                AssertEx.Equal(0, Grid.GetColumn(modeSelector), "The mode selector should occupy the first inline column.");
                AssertEx.True(
                    inlineGrid.Children.OfType<ContentControl>().Any(x => Grid.GetColumn(x) == 1),
                    "The fixed-value or variable editor should occupy the second inline column.");
                modeSelector.SelectedIndex = 1;
                AssertEx.Equal(NodeSettingValueMode.Variable, editableNode.Settings["Message"].Mode,
                    "Switching the setting mode should store Variable in the setting itself.");
                AssertEx.Equal("hello", editableNode.Settings["Message"].ConstantValue,
                    "Switching to variable mode should preserve the previous constant value.");

                var editableVariableSelector = FindChildren<VariableSelectorControl>(editablePanel).FirstOrDefault();
                AssertEx.NotNull(editableVariableSelector, "Variable mode should replace the constant editor with a structured variable selector.");
                editableVariableSelector.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, editableVariableSelector));
                var sourceGroup = editableVariableSelector.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(x => x.Items.Count > 0);
                AssertEx.NotNull(sourceGroup, "The variable selector should group compatible variables by source.");
                var sourceItem = sourceGroup.Items.OfType<MenuItem>().FirstOrDefault();
                AssertEx.NotNull(sourceItem, "The variable selector should show a structured source/output/type item.");
                sourceItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, sourceItem));
                AssertEx.Equal("source", editableNode.Settings["Message"].Selector.Path[0],
                    "Selecting an item should persist its structured node-output path.");
                AssertEx.False(FindChildren<TextBlock>(editablePanel).Any(x => (x.Text ?? string.Empty).IndexOf("请选择变量", StringComparison.Ordinal) >= 0),
                    "Selecting a valid item should clear the incomplete-variable error immediately.");

                modeSelector.SelectedIndex = 0;
                AssertEx.Equal(NodeSettingValueMode.Constant, editableNode.Settings["Message"].Mode,
                    "Switching back should restore constant mode.");
                AssertEx.Equal("hello", editableNode.Settings["Message"].ConstantValue,
                    "Switching back should restore the preserved constant value.");

                var policyDescriptor = CreateDescriptor();
                policyDescriptor.Outputs.Add(new NodeOutputDescriptor
                {
                    Name = "Result",
                    DisplayName = "结果",
                    DataType = FlowDataType.String
                });
                policyDescriptor.Outputs.Add(new NodeOutputDescriptor
                {
                    Name = "Count",
                    DisplayName = "数量",
                    DataType = FlowDataType.Int32
                });
                policyDescriptor.Outputs.Add(new NodeOutputDescriptor
                {
                    Name = "Score",
                    DisplayName = "分数",
                    DataType = FlowDataType.Double
                });
                policyDescriptor.Outputs.Add(new NodeOutputDescriptor
                {
                    Name = "Passed",
                    DisplayName = "通过",
                    DataType = FlowDataType.Boolean
                });

                var policyNode = CreateNode();
                var policyChanges = 0;
                var policyPanel = new PropertyPanelControl();
                policyPanel.ShowNode(policyNode, policyDescriptor, null, delegate { policyChanges++; }, false);
                AssertEx.True(FindChildren<TextBlock>(policyPanel).Any(x => string.Equals(x.Text, "执行策略", StringComparison.Ordinal)),
                    "Every node should expose the common execution-policy section.");
                AssertEx.False(policyNode.ExecutionPolicy.RetryPolicy.Enabled,
                    "Retry should be disabled by default.");
                AssertEx.Equal(3, policyNode.ExecutionPolicy.RetryPolicy.MaxRetries,
                    "The Dify-style retry editor should start with three retries.");
                AssertEx.Equal(1000, policyNode.ExecutionPolicy.RetryPolicy.RetryIntervalMs,
                    "The Dify-style retry editor should start with a 1000 ms interval.");

                var retryToggle = FindChildren<CheckBox>(policyPanel)
                    .FirstOrDefault(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.RetryPolicy.Enabled", StringComparison.Ordinal));
                AssertEx.NotNull(retryToggle, "The execution policy should render an Enable Retry switch.");
                AssertEx.False(FindChildren<TextBox>(policyPanel).Any(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.RetryPolicy.MaxRetries",
                        StringComparison.Ordinal)),
                    "Retry details should stay hidden while retry is disabled.");
                retryToggle.IsChecked = true;
                AssertEx.True(policyNode.ExecutionPolicy.RetryPolicy.Enabled,
                    "Turning on retry should persist RetryPolicy.Enabled.");

                var timeoutEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.TimeoutMs", StringComparison.Ordinal));
                timeoutEditor.Text = "2500";
                timeoutEditor.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, timeoutEditor));
                var concurrencyEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.MaxConcurrentExecutions", StringComparison.Ordinal));
                concurrencyEditor.Text = "4";
                concurrencyEditor.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, concurrencyEditor));
                var maxRetriesEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.RetryPolicy.MaxRetries", StringComparison.Ordinal));
                maxRetriesEditor.Text = "5";
                maxRetriesEditor.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, maxRetriesEditor));
                var retryIntervalEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.RetryPolicy.RetryIntervalMs", StringComparison.Ordinal));
                retryIntervalEditor.Text = "750";
                retryIntervalEditor.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, retryIntervalEditor));
                AssertEx.Equal(2500, policyNode.ExecutionPolicy.TimeoutMs,
                    "TimeoutMs should persist from the static execution-policy editor.");
                AssertEx.Equal(4, policyNode.ExecutionPolicy.MaxConcurrentExecutions,
                    "MaxConcurrentExecutions should persist from the static execution-policy editor.");
                AssertEx.Equal(5, policyNode.ExecutionPolicy.RetryPolicy.MaxRetries,
                    "MaxRetries should persist from the Dify-style retry editor.");
                AssertEx.Equal(750, policyNode.ExecutionPolicy.RetryPolicy.RetryIntervalMs,
                    "RetryIntervalMs should persist from the Dify-style retry editor.");

                var failureSelector = FindChildren<ComboBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.FailureStrategy", StringComparison.Ordinal));
                failureSelector.SelectedIndex = 2;
                AssertEx.Equal(FailureStrategy.DefaultOutputs, policyNode.ExecutionPolicy.FailureStrategy,
                    "Switching to default outputs should persist the failure strategy.");
                AssertEx.True(FindChildren<TextBlock>(policyPanel).Any(x => (x.Text ?? string.Empty).IndexOf("常量回退输出", StringComparison.Ordinal) >= 0),
                    "DefaultOutputs should explain that constant fallback values continue through Next.");

                var resultEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.DefaultOutputs.Result", StringComparison.Ordinal));
                resultEditor.Text = "fallback";
                resultEditor.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, resultEditor));
                var countEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.DefaultOutputs.Count", StringComparison.Ordinal));
                countEditor.Text = "42";
                countEditor.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, countEditor));
                var scoreEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.DefaultOutputs.Score", StringComparison.Ordinal));
                scoreEditor.Text = "1.5";
                scoreEditor.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, scoreEditor));
                var passedEditor = FindChildren<CheckBox>(policyPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.DefaultOutputs.Passed", StringComparison.Ordinal));
                passedEditor.IsChecked = true;
                AssertEx.Equal("fallback", policyNode.ExecutionPolicy.DefaultOutputs["Result"],
                    "String fallback output should persist as a constant string.");
                AssertEx.Equal(42, policyNode.ExecutionPolicy.DefaultOutputs["Count"],
                    "Int32 fallback output should be converted before persistence.");
                AssertEx.Equal(1.5d, policyNode.ExecutionPolicy.DefaultOutputs["Score"],
                    "Double fallback output should be converted before persistence.");
                AssertEx.Equal(true, policyNode.ExecutionPolicy.DefaultOutputs["Passed"],
                    "Boolean fallback output should be converted before persistence.");

                failureSelector.SelectedIndex = 1;
                AssertEx.True(FindChildren<TextBlock>(policyPanel).Any(x => (x.Text ?? string.Empty).IndexOf("Error 或 Timeout", StringComparison.Ordinal) >= 0),
                    "ErrorBranch should explain its control-port continuation behavior.");
                AssertEx.False(FindChildren<TextBox>(policyPanel).Any(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.DefaultOutputs.Result",
                        StringComparison.Ordinal)),
                    "Default-output editors should be hidden outside DefaultOutputs mode.");
                failureSelector.SelectedIndex = 0;
                AssertEx.True(FindChildren<TextBlock>(policyPanel).Any(x => (x.Text ?? string.Empty).IndexOf("停止本次流程", StringComparison.Ordinal) >= 0),
                    "StopFlow should explain that the current flow run stops.");
                failureSelector.SelectedIndex = 2;
                AssertEx.Equal("fallback", policyNode.ExecutionPolicy.DefaultOutputs["Result"],
                    "Switching failure modes should preserve existing fallback constants.");

                var retryCard = new NodeCardControl(new NodeViewModel(policyNode, policyDescriptor));
                AssertEx.True(FindChildren<TextBlock>(retryCard).Any(x => string.Equals(x.Text, "重试", StringComparison.Ordinal)),
                    "An enabled retry policy should add a Chinese retry summary to the node card.");
                AssertEx.True(FindChildren<TextBlock>(retryCard).Any(x => string.Equals(x.Text, "5 次 · 750 ms", StringComparison.Ordinal)),
                    "The node-card retry summary should show retry count and interval.");
                policyNode.ExecutionPolicy.RetryPolicy.Enabled = false;
                retryCard.UpdateSummary();
                AssertEx.False(FindChildren<TextBlock>(retryCard).Any(x => string.Equals(x.Text, "重试", StringComparison.Ordinal)),
                    "Disabling retry should remove its node-card summary.");

                policyNode.ExecutionPolicy.RetryPolicy.Enabled = true;
                var readOnlyPolicyPanel = new NodeExecutionPolicyPanelControl();
                readOnlyPolicyPanel.ShowPolicy(policyNode, policyDescriptor, delegate { }, true);
                AssertEx.True(FindChildren<TextBox>(readOnlyPolicyPanel).All(x => x.IsReadOnly),
                    "Read-only mode should make every execution-policy TextBox read-only.");
                AssertEx.True(FindChildren<ComboBox>(readOnlyPolicyPanel).All(x => !x.IsEnabled),
                    "Read-only mode should disable failure-strategy selectors.");
                AssertEx.True(FindChildren<CheckBox>(readOnlyPolicyPanel).All(x => !x.IsEnabled),
                    "Read-only mode should disable retry and Boolean fallback switches.");
                AssertEx.Equal(0, FindChildren<VariableSelectorControl>(readOnlyPolicyPanel).Count(),
                    "Execution policies and fallback outputs must never create variable selectors.");
                AssertEx.True(policyChanges >= 10,
                    "Execution-policy edits should notify the designer so cards and persistence state refresh.");

                var flow = new RuntimeFlowDefinition();
                flow.Edges.Add(new EdgeDefinition { FromNodeId = "a", ToNodeId = "b" });
                flow.Edges.Add(new EdgeDefinition { FromNodeId = "b", ToNodeId = "c" });
                flow.Edges.Add(new EdgeDefinition { FromNodeId = "d", ToNodeId = "c" });
                flow.Edges.Add(new EdgeDefinition { FromNodeId = "x", ToNodeId = "y" });
                var ancestors = InvokePrivateStatic<HashSet<string>>(
                    typeof(FlowDesignerControl),
                    "FindAncestorNodeIds",
                    flow,
                    "c");
                AssertEx.True(ancestors.SetEquals(new[] { "a", "b", "d" }),
                    "Variable candidates should come from every direct and indirect ancestor, excluding unrelated nodes and the current node.");

                var sourceSetting = NodeSettingValue.ForConstant(new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "Name", "Exposure" }, { "Value", 1000 } }
                });
                var clonedSetting = InvokePrivateStatic<NodeSettingValue>(
                    typeof(FlowDesignerControl),
                    "CloneSettingValue",
                    sourceSetting);
                var clonedItems = (System.Collections.IList)clonedSetting.ConstantValue;
                var clonedItem = (System.Collections.IDictionary)clonedItems[0];
                clonedItem["Value"] = 2000;
                var sourceItems = (List<Dictionary<string, object>>)sourceSetting.ConstantValue;
                AssertEx.Equal(1000, sourceItems[0]["Value"],
                    "Duplicating a node should deep-copy collection and dictionary constant values.");

                var candidateDocument = new FlowDesignDocument
                {
                    Runtime = new RuntimeFlowDefinition(),
                    View = new FlowViewState()
                };
                var a = new NodeDefinition { Id = "entry_a", Type = "test.node" };
                var b = new NodeDefinition { Id = "entry_b", Type = "test.node" };
                var c = new NodeDefinition { Id = "target", Type = "test.node" };
                candidateDocument.Runtime.Nodes.AddRange(new[] { a, b, c });
                candidateDocument.Runtime.Edges.Add(new EdgeDefinition { FromNodeId = a.Id, ToNodeId = c.Id });
                candidateDocument.Runtime.Edges.Add(new EdgeDefinition { FromNodeId = b.Id, ToNodeId = c.Id });
                candidateDocument.Runtime.Edges.Add(new EdgeDefinition { FromNodeId = "event_source", ToNodeId = c.Id });
                candidateDocument.Runtime.Entries.Add(new FlowEntryDefinition
                {
                    EntryName = "A",
                    TargetNodeId = a.Id,
                    Inputs =
                    {
                        new TriggerInputDescriptor { Name = "Shared", DisplayName = "共享输入", DataType = FlowDataType.String },
                        new TriggerInputDescriptor { Name = "Conflict", DataType = FlowDataType.Int32 }
                    }
                });
                candidateDocument.Runtime.Entries.Add(new FlowEntryDefinition
                {
                    EntryName = "B",
                    TargetNodeId = b.Id,
                    Inputs =
                    {
                        new TriggerInputDescriptor { Name = "Shared", DisplayName = "共享输入", DataType = FlowDataType.String },
                        new TriggerInputDescriptor { Name = "Conflict", DataType = FlowDataType.String }
                    }
                });
                candidateDocument.Runtime.Entries.Add(new FlowEntryDefinition
                {
                    EntryName = "FrameEvent",
                    TriggerKind = FlowTriggerKind.NodeEvent,
                    SourceNodeId = "event_source",
                    TargetNodeId = "not_reachable_from_target",
                    Inputs =
                    {
                        new TriggerInputDescriptor { Name = "EventOnly", DataType = FlowDataType.Boolean }
                    }
                });
                var candidateControl = new FlowDesignerControl(null, new FlowDesignerOptions { LoadSampleOnStartup = false });
                SetPrivateField(candidateControl, "_document", candidateDocument);
                var triggerOptions = new List<VariableSelectionOption>();
                var triggerIssues = new List<string>();
                InvokePrivate(candidateControl, "AddTriggerInputVariableSuggestions", triggerOptions, triggerIssues, c);
                AssertEx.Equal(2, triggerOptions.Count,
                    "Reachable trigger inputs should be included while same-name/same-type inputs are deduplicated.");
                var sharedOption = triggerOptions.First(x => string.Equals(x.Selector.Path[0], "Shared", StringComparison.OrdinalIgnoreCase));
                AssertEx.Equal(VariableSelectorScope.TriggerInput, sharedOption.Selector.Scope,
                    "Entry inputs should become TriggerInput variable candidates.");
                AssertEx.Equal("Shared", sharedOption.Selector.Path[0],
                    "A TriggerInput candidate should persist the stable input name in its path.");
                AssertEx.True(triggerOptions.Any(x => string.Equals(x.Selector.Path[0], "EventOnly", StringComparison.OrdinalIgnoreCase)),
                    "NodeEvent inputs should use SourceNodeId as their reachability origin because execution continues along the source node's outgoing edges.");
                AssertEx.True(triggerIssues.Any(x => x.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Conflicting reachable input types should be excluded and reported.");
                var conflictPanel = new PropertyPanelControl();
                conflictPanel.ShowNode(CreateNode(), descriptor, triggerOptions, triggerIssues, delegate { }, false);
                AssertEx.True(FindChildren<TextBlock>(conflictPanel).Any(x => (x.Text ?? string.Empty).IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Trigger-input conflicts should be visible in the property panel.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyPanelUsesHostProvidedConstantOptions()
        {
            RunOnSta(delegate
            {
                var descriptor = CreateDescriptor();
                descriptor.Settings[0].Name = "CameraId";
                descriptor.Settings[0].DisplayName = "相机";
                descriptor.Settings[0].BindingMode = NodeSettingBindingMode.ConstantOnly;
                var node = CreateNode();
                node.Settings.Clear();
                node.Settings["CameraId"] = NodeSettingValue.ForConstant("Camera-B");

                var panel = new PropertyPanelControl(setting =>
                    string.Equals(setting.Name, "CameraId", StringComparison.OrdinalIgnoreCase)
                        ? new[]
                        {
                            new NodeSettingConstantOption("Camera-A", "Camera-A"),
                            new NodeSettingConstantOption("Camera-B", "Camera-B")
                        }
                        : null);
                panel.ShowNode(node, descriptor, delegate { });

                var valueSelector = FindChildren<ComboBox>(panel)
                    .FirstOrDefault(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:CameraId",
                        StringComparison.Ordinal));
                AssertEx.NotNull(valueSelector, "Host-provided camera ids should render a fixed-value selector.");
                AssertEx.False(valueSelector.IsEditable,
                    "Host-provided options should restrict the fixed value to configured items.");
                AssertEx.True(valueSelector.Items.Cast<object>().Any(x => string.Equals(Convert.ToString(x), "Camera-A", StringComparison.Ordinal)),
                    "The selector should contain the first configured camera.");
                AssertEx.True(valueSelector.Items.Cast<object>().Any(x => string.Equals(Convert.ToString(x), "Camera-B", StringComparison.Ordinal)),
                    "The selector should contain the second configured camera.");
                AssertEx.False(valueSelector.Items.Cast<object>().Any(x => string.Equals(Convert.ToString(x), "Camera01", StringComparison.Ordinal)),
                    "The designer should not inject a hard-coded camera id.");

                var emptyPanel = new PropertyPanelControl(setting => new NodeSettingConstantOption[0]);
                emptyPanel.ShowNode(node, descriptor, delegate { });
                var emptySelector = FindChildren<ComboBox>(emptyPanel)
                    .FirstOrDefault(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:CameraId",
                        StringComparison.Ordinal));
                AssertEx.NotNull(emptySelector,
                    "An empty host data source should remain an empty selector instead of falling back to free text.");
                AssertEx.False(emptySelector.IsEditable,
                    "An empty host-backed selector must not allow free text.");
                AssertEx.Equal(0, emptySelector.Items.Count,
                    "An empty host-backed selector should keep an empty candidate list.");
                AssertEx.False(FindChildren<Button>(emptyPanel).Any(x =>
                        string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "固定值", StringComparison.Ordinal) ||
                        string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "变量", StringComparison.Ordinal)),
                    "A constant-only device reference with no candidates must not expose a mode selector.");

                descriptor.Settings[0].Name = "QueueId";
                descriptor.Settings[0].DisplayName = "队列";
                descriptor.Settings[0].DataType = FlowDataType.Int32;
                node.Settings.Clear();
                node.Settings["QueueId"] = NodeSettingValue.ForConstant(2);
                var labeledPanel = new PropertyPanelControl(
                    setting => string.Equals(setting.Name, "QueueId", StringComparison.OrdinalIgnoreCase)
                        ? new[]
                        {
                            new NodeSettingConstantOption("1", "拍照信息队列"),
                            new NodeSettingConstantOption("2", "待处理图片队列")
                        }
                        : null);
                labeledPanel.ShowNode(node, descriptor, delegate { });
                var labeledSelector = FindChildren<ComboBox>(labeledPanel)
                    .FirstOrDefault(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:QueueId",
                        StringComparison.Ordinal));
                AssertEx.NotNull(labeledSelector,
                    "Labeled host options should render a fixed-value selector.");
                AssertEx.Equal("待处理图片队列", Convert.ToString(labeledSelector.SelectedItem),
                    "The selector should display the label that belongs to the stored protocol value.");
                labeledSelector.SelectedIndex = 0;
                AssertEx.Equal(
                    1,
                    Convert.ToInt32(node.Settings["QueueId"].ConstantValue, CultureInfo.InvariantCulture),
                    "Selecting a labeled option should persist its converted protocol value.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyPanelUsesEnumConstantsAndExactEnumVariables()
        {
            RunOnSta(delegate
            {
                var descriptor = CreateDescriptor();
                var setting = descriptor.Settings.First(x =>
                    string.Equals(x.Name, "Message", StringComparison.Ordinal));
                setting.EnumType = typeof(TestOperatingMode);
                var node = CreateNode();
                node.Settings["Message"] = NodeSettingValue.ForConstant("Automatic");
                var exact = new VariableSelectionOption(
                    VariableSelector.ForNodeOutput("exact", "Mode"),
                    "Exact [exact]",
                    "Exact",
                    "exact",
                    "Mode",
                    FlowDataType.String,
                    typeof(TestOperatingMode));
                var otherEnum = new VariableSelectionOption(
                    VariableSelector.ForNodeOutput("other", "Mode"),
                    "Other [other]",
                    "Other",
                    "other",
                    "Mode",
                    FlowDataType.String,
                    typeof(TestFailureMode));
                var plainString = new VariableSelectionOption(
                    VariableSelector.ForNodeOutput("plain", "Text"),
                    "Plain [plain]",
                    "Plain",
                    "plain",
                    "Text",
                    FlowDataType.String);
                var panel = new PropertyPanelControl();
                panel.ShowNode(
                    node,
                    descriptor,
                    new[] { exact, otherEnum, plainString },
                    delegate { },
                    false);

                var constantSelector = FindChildren<ComboBox>(panel)
                    .FirstOrDefault(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:Message",
                        StringComparison.Ordinal));
                AssertEx.NotNull(constantSelector,
                    "An enum setting should render a constant selector without host options.");
                AssertEx.Equal(
                    Enum.GetNames(typeof(TestOperatingMode)).Length,
                    constantSelector.Items.Count,
                    "The enum constant selector should contain every declared member.");
                AssertEx.Equal(
                    "Automatic",
                    Convert.ToString(constantSelector.SelectedItem, CultureInfo.InvariantCulture),
                    "The enum selector should select the stored wire value.");

                var modeSelector = FindChildren<ComboBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Message:Mode",
                        StringComparison.Ordinal));
                modeSelector.SelectedIndex = 1;
                var variableSelector = FindChildren<VariableSelectorControl>(panel).Single();
                variableSelector.RaiseEvent(new RoutedEventArgs(
                    Button.ClickEvent,
                    variableSelector));
                var selectableItems = variableSelector.ContextMenu.Items
                    .OfType<MenuItem>()
                    .SelectMany(x => x.Items.OfType<MenuItem>())
                    .ToList();
                AssertEx.Equal(
                    1,
                    selectableItems.Count,
                    "An enum setting should expose only variables with the same concrete enum type.");
                AssertEx.True(
                    Convert.ToString(selectableItems[0].Header, CultureInfo.InvariantCulture)
                        .IndexOf("Exact", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The matching enum variable should remain selectable.");
            });
            return Task.FromResult(0);
        }

        public static Task ConditionEditorFollowsBoundOperandType()
        {
            RunOnSta(delegate
            {
                var descriptor = ConditionNodeDescriptor.Create();
                var enumOption = new VariableSelectionOption(
                    VariableSelector.ForNodeOutput("motion", "CaptureSequencePosition"),
                    "运控开始拍照 [motion] / 拍照位置",
                    "运控开始拍照",
                    "motion",
                    "CaptureSequencePosition",
                    FlowDataType.String,
                    typeof(TestCaptureSequencePosition));
                var node = new NodeDefinition
                {
                    Id = "condition1",
                    Type = ConditionNodeFactory.TypeName,
                    Name = "IF",
                    Version = "3.0.0",
                    Settings =
                    {
                        { FlowSettingNames.LeftValue, NodeSettingValue.ForVariable(enumOption.Selector) },
                        { FlowSettingNames.Operator, NodeSettingValue.ForConstant(ConditionOperator.Equal) },
                        { FlowSettingNames.RightValue, NodeSettingValue.ForConstant("First") }
                    }
                };
                var panel = new PropertyPanelControl();
                panel.ShowNode(node, descriptor, new[] { enumOption }, delegate { }, false);

                AssertEx.False(FindChildren<ComboBox>(panel).Any(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        FlowSettingNames.LeftValue + ":Mode",
                        StringComparison.Ordinal)),
                    "The condition left operand should be variable-only and must not expose a fixed-value mode.");
                var operatorSelector = FindChildren<ComboBox>(panel).First(x => string.Equals(
                    Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                    "Setting:" + FlowSettingNames.Operator,
                    StringComparison.Ordinal));
                AssertEx.Equal(2, operatorSelector.Items.Count,
                    "Enum operands should expose only equality and inequality operators.");
                var rightSelector = FindChildren<ComboBox>(panel).First(x => string.Equals(
                    Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                    "Setting:" + FlowSettingNames.RightValue,
                    StringComparison.Ordinal));
                AssertEx.SequenceEqual(
                    Enum.GetNames(typeof(TestCaptureSequencePosition)),
                    rightSelector.Items.Cast<object>().Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)),
                    "A fixed enum right operand should list all members from the bound left output's enum type.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyPanelUsesModernEditorTypesAndSeparatedSegments()
        {
            RunOnSta(delegate
            {
                var descriptor = CreateDescriptor();
                var node = CreateNode();
                var option = new VariableSelectionOption(
                    VariableSelector.ForNodeOutput("source", "Image"),
                    "Source [source]",
                    "Source",
                    "source",
                    "Image",
                    FlowDataType.String);
                PropertyPanelControl panel = null;
                panel = new PropertyPanelControl();
                panel.ShowNode(node, descriptor, new[] { option }, delegate
                {
                    panel.SetPendingState(true, false);
                }, false);
                ArrangeAtPropertyPanelMinimum(panel);

                var messageEditor = FindChildren<TextBox>(panel)
                    .FirstOrDefault(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:Message",
                        StringComparison.Ordinal));
                AssertEx.NotNull(messageEditor,
                    "An ordinary fixed string value should use a manually editable TextBox.");
                AssertEx.False(FindChildren<ComboBox>(panel).Any(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:Message",
                        StringComparison.Ordinal)),
                    "An ordinary fixed value must not become a ComboBox without an explicit option provider.");

                var constantSegment = FindChildren<Button>(panel)
                    .First(x => string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "固定值", StringComparison.Ordinal));
                var variableSegment = FindChildren<Button>(panel)
                    .First(x => string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "变量", StringComparison.Ordinal));
                AssertHorizontalSeparation(panel, constantSegment, variableSegment, 5,
                    "Fixed and variable segment buttons must keep a visible gap instead of sharing or overlapping borders.");
                AssertEx.True(
                    Math.Abs(constantSegment.ActualHeight - 40) < 0.01 &&
                    Math.Abs(variableSegment.ActualHeight - 40) < 0.01 &&
                    Math.Abs(messageEditor.ActualHeight - 40) < 0.01,
                    "Mode buttons and the fixed-value editor should share the 40 px field height.");
                AssertTopAligned(
                    panel,
                    constantSegment,
                    messageEditor,
                    "The fixed-value mode button and its editor should start on the same row edge.");
                AssertTopAligned(
                    panel,
                    variableSegment,
                    messageEditor,
                    "The variable mode button and the fixed-value editor should start on the same row edge.");

                var modeSelector = FindChildren<ComboBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Message:Mode",
                        StringComparison.Ordinal));
                modeSelector.SelectedIndex = 1;
                ArrangeAtPropertyPanelMinimum(panel);
                var applyButton = GetPrivateField<Button>(panel, "_applyButton");
                AssertEx.False(applyButton.IsEnabled,
                    "Switching to variable mode without a selector should immediately disable Apply.");
                var variableSelector = FindChildren<VariableSelectorControl>(panel).FirstOrDefault();
                AssertEx.NotNull(variableSelector,
                    "Variable mode should render the dedicated variable selector.");
                var variableSelectorStyle = variableSelector.FindResource(
                    FlowDesignerTheme.VariableSelectorButtonStyleKey) as Style;
                AssertEx.True(variableSelectorStyle != null &&
                    object.ReferenceEquals(variableSelectorStyle, variableSelector.Style),
                    "The variable selector should resolve an explicit modern button style.");
                AssertEx.True(variableSelector.Template != null,
                    "The variable selector style should own a custom control template.");
                AssertEx.True(Math.Abs(variableSelector.ActualHeight - 40) < 0.01,
                    "The variable selector should align to the shared 40 px field height.");
                AssertTopAligned(
                    panel,
                    constantSegment,
                    variableSelector,
                    "The mode buttons and variable selector should stay top-aligned after switching modes.");
                AssertEx.True(FindChildren<System.Windows.Shapes.Path>(variableSelector).Any(),
                    "The variable selector should render its dropdown affordance as a vector path.");
                variableSelector.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, variableSelector));
                var variableGroup = variableSelector.ContextMenu.Items
                    .OfType<MenuItem>()
                    .First(x => x.Items.Count > 0);
                var variableItem = variableGroup.Items.OfType<MenuItem>().First();
                variableItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, variableItem));
                AssertEx.True(applyButton.IsEnabled,
                    "Selecting a compatible variable should immediately clear the error and re-enable Apply.");

                descriptor.Settings[0].Name = "CameraId";
                descriptor.Settings[0].DisplayName = "相机";
                descriptor.Settings[0].BindingMode = NodeSettingBindingMode.ConstantOnly;
                node.Settings.Clear();
                node.Settings["CameraId"] = NodeSettingValue.ForConstant("Camera-B");
                var cameraPanel = new PropertyPanelControl(setting =>
                    string.Equals(setting.Name, "CameraId", StringComparison.OrdinalIgnoreCase)
                        ? new[]
                        {
                            new NodeSettingConstantOption("Camera-A", "Camera-A"),
                            new NodeSettingConstantOption("Camera-B", "Camera-B")
                        }
                        : null);
                cameraPanel.ShowNode(node, descriptor, delegate { });
                ArrangeAtPropertyPanelMinimum(cameraPanel);
                var cameraSelector = FindChildren<ComboBox>(cameraPanel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:CameraId",
                        StringComparison.Ordinal));
                cameraSelector.ApplyTemplate();
                AssertEx.True(Math.Abs(cameraSelector.ActualHeight - 40) < 0.01,
                    "A host-backed selector should share the 40 px field height.");
                AssertEx.True(cameraSelector.Template != null,
                    "A host-backed fixed value should use the modern ComboBox template.");
                AssertEx.NotNull(cameraSelector.Template.FindName("PART_Popup", cameraSelector),
                    "The modern ComboBox template should provide its own popup.");
                AssertEx.True(FindChildren<System.Windows.Shapes.Path>(cameraSelector).Any(),
                    "The modern ComboBox should draw its arrow as a vector path.");
                AssertEx.True(cameraSelector.ItemContainerStyle != null &&
                    cameraSelector.ItemContainerStyle.Setters.OfType<Setter>()
                        .Any(x => x.Property == Control.TemplateProperty),
                    "The modern ComboBox should style its dropdown candidates instead of using native rows.");
                AssertEx.False(FindChildren<Button>(cameraPanel).Any(x =>
                        string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "固定值", StringComparison.Ordinal) ||
                        string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "变量", StringComparison.Ordinal)),
                    "A constant-only device reference should not expose fixed/variable mode controls.");
            });
            return Task.FromResult(0);
        }

        private enum TestOperatingMode
        {
            Manual = 0,
            Automatic = 1
        }

        private enum TestFailureMode
        {
            Stop = 0,
            Continue = 1
        }

        private enum TestCaptureSequencePosition
        {
            Intermediate = 0,
            First = 1,
            Last = 2
        }

        public static Task PropertyTextEditorsKeepSingleAndMultilineLayoutRules()
        {
            RunOnSta(delegate
            {
                var descriptor = CreateDescriptor();
                descriptor.Settings[0].IsRequired = true;
                descriptor.Settings.Add(new NodeSettingDescriptor
                {
                    Name = "FieldMappings",
                    DisplayName = "字段映射",
                    DataType = FlowDataType.String
                });
                var node = CreateNode();
                node.Settings["FieldMappings"] = NodeSettingValue.ForConstant("Result=Value");
                var panel = new PropertyPanelControl();
                panel.ShowNode(node, descriptor, delegate { });
                ArrangeAtPropertyPanelMinimum(panel);

                var ordinaryEditor = FindChildren<TextBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:Message",
                        StringComparison.Ordinal));
                var followingLabel = FindChildren<TextBlock>(panel)
                    .First(x => string.Equals(x.Text, "Enabled (Enabled)", StringComparison.Ordinal));
                var editorBefore = GetBoundsRelativeTo(ordinaryEditor, panel);
                var followingBefore = GetBoundsRelativeTo(followingLabel, panel);

                ordinaryEditor.Text = "ExposureTimeExposureTimeExposureTimeExposureTimeExposureTime";
                ArrangeAtPropertyPanelMinimum(panel);

                AssertEx.Equal(TextWrapping.NoWrap, ordinaryEditor.TextWrapping,
                    "Ordinary fixed values should remain single-line editors.");
                AssertEx.False(ordinaryEditor.AcceptsReturn,
                    "Ordinary fixed values should not accept line breaks.");
                AssertEx.True(Math.Abs(ordinaryEditor.ActualHeight - 40) < 0.01,
                    "A long ordinary value must keep the shared 40 px editor height.");
                AssertEx.True(
                    Math.Abs(ordinaryEditor.Padding.Top) < 0.01 &&
                    Math.Abs(ordinaryEditor.Padding.Bottom) < 0.01,
                    "A fixed-height single-line editor must not reduce its text viewport with vertical padding.");
                AssertEx.Equal(VerticalAlignment.Center, ordinaryEditor.VerticalContentAlignment,
                    "A single-line editor should vertically center its full text viewport.");
                ordinaryEditor.ApplyTemplate();
                var ordinaryContentHost = ordinaryEditor.Template.FindName("PART_ContentHost", ordinaryEditor)
                    as ScrollViewer;
                AssertEx.NotNull(ordinaryContentHost,
                    "The modern text editor template should expose its content host.");
                AssertEx.True(
                    ordinaryContentHost.ActualHeight >= ordinaryEditor.FontSize * 2,
                    "The single-line text viewport should remain tall enough to render complete glyphs.");
                AssertPositionUnchanged(
                    editorBefore,
                    GetBoundsRelativeTo(ordinaryEditor, panel),
                    "A long ordinary value must not move its editor.");
                AssertSizeUnchanged(
                    editorBefore,
                    GetBoundsRelativeTo(ordinaryEditor, panel),
                    "A long ordinary value must not resize its editor.");
                AssertPositionUnchanged(
                    followingBefore,
                    GetBoundsRelativeTo(followingLabel, panel),
                    "A long ordinary value must not push following fields.");

                ordinaryEditor.Text = string.Empty;
                ArrangeAtPropertyPanelMinimum(panel);
                var validationOutline = FindChildren<Border>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ValidationOutline:Setting:Message",
                        StringComparison.Ordinal));
                var inlineError = FindChildren<TextBlock>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "EditorError:Setting:Message",
                        StringComparison.Ordinal));
                AssertEx.Equal(Visibility.Visible, validationOutline.Visibility,
                    "An invalid ordinary value should show its validation outline.");
                AssertPositionUnchanged(
                    GetBoundsRelativeTo(ordinaryEditor, panel),
                    GetBoundsRelativeTo(validationOutline, panel),
                    "The validation outline should cover only the 40 px editor.");
                AssertSizeUnchanged(
                    GetBoundsRelativeTo(ordinaryEditor, panel),
                    GetBoundsRelativeTo(validationOutline, panel),
                    "The validation outline must not include the reserved error slot.");
                AssertVerticalSeparation(
                    panel,
                    ordinaryEditor,
                    inlineError,
                    2,
                    "The fixed validation slot should remain below the 40 px editor.");
                AssertTopAligned(
                    panel,
                    ordinaryEditor,
                    FindChildren<Button>(panel)
                        .First(x => string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "固定值", StringComparison.Ordinal)),
                    "Validation must not disturb row alignment.");

                var multilineEditor = FindChildren<TextBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:FieldMappings",
                        StringComparison.Ordinal));
                AssertEx.Equal(TextWrapping.Wrap, multilineEditor.TextWrapping,
                    "Explicit mappings fields should retain wrapped multiline editing.");
                AssertEx.True(multilineEditor.AcceptsReturn,
                    "Explicit mappings fields should continue accepting line breaks.");
                AssertEx.True(multilineEditor.MinHeight >= 76,
                    "Explicit mappings fields should retain their multiline minimum height.");
                AssertEx.True(
                    multilineEditor.Padding.Top >= 8 &&
                    multilineEditor.Padding.Bottom >= 8,
                    "Explicit multiline fields should retain comfortable vertical padding.");
                AssertEx.Equal(VerticalAlignment.Top, multilineEditor.VerticalContentAlignment,
                    "Explicit multiline fields should start text at the top.");
                AssertEx.Equal(ScrollBarVisibility.Auto, multilineEditor.VerticalScrollBarVisibility,
                    "Explicit mappings fields should retain an automatic vertical scrollbar.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyValidationSlotsKeepEditorPositionsStable()
        {
            RunOnSta(delegate
            {
                var descriptor = CreateDescriptor();
                descriptor.Settings[0].IsRequired = true;
                var node = CreateNode();
                var option = new VariableSelectionOption(
                    VariableSelector.ForNodeOutput("source", "Image"),
                    "Source [source]",
                    "Source",
                    "source",
                    "Image",
                    FlowDataType.String);
                var panel = new PropertyPanelControl();
                panel.ShowNode(node, descriptor, new[] { option }, delegate { }, false);
                ArrangeAtPropertyPanelMinimum(panel);

                var scrollViewer = GetPrivateField<ScrollViewer>(panel, "_scrollViewer");
                var applyButton = GetPrivateField<Button>(panel, "_applyButton");
                var footer = FindAncestor<Border>(applyButton);
                var scrollBeforeSummary = GetBoundsRelativeTo(scrollViewer, panel);
                var footerBeforeSummary = GetBoundsRelativeTo(footer, panel);
                var applyBeforeSummary = GetBoundsRelativeTo(applyButton, panel);
                panel.ShowValidationError("属性校验失败，请检查当前输入。");
                ArrangeAtPropertyPanelMinimum(panel);
                AssertPositionUnchanged(
                    scrollBeforeSummary,
                    GetBoundsRelativeTo(scrollViewer, panel),
                    "Showing the validation summary must not move or resize the form viewport.");
                AssertSizeUnchanged(
                    scrollBeforeSummary,
                    GetBoundsRelativeTo(scrollViewer, panel),
                    "Showing the validation summary must not resize the form viewport.");
                AssertPositionUnchanged(
                    footerBeforeSummary,
                    GetBoundsRelativeTo(footer, panel),
                    "Showing the validation summary must not move the footer.");
                AssertSizeUnchanged(
                    footerBeforeSummary,
                    GetBoundsRelativeTo(footer, panel),
                    "Showing the validation summary must not resize the footer.");
                AssertPositionUnchanged(
                    applyBeforeSummary,
                    GetBoundsRelativeTo(applyButton, panel),
                    "Showing the validation summary must not move footer actions.");

                var messageEditor = FindChildren<TextBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:Message",
                        StringComparison.Ordinal));
                var followingLabel = FindChildren<TextBlock>(panel)
                    .First(x => string.Equals(x.Text, "Enabled (Enabled)", StringComparison.Ordinal));
                var editorBeforeError = GetBoundsRelativeTo(messageEditor, panel);
                var followingBeforeError = GetBoundsRelativeTo(followingLabel, panel);

                messageEditor.Text = string.Empty;
                ArrangeAtPropertyPanelMinimum(panel);
                AssertPositionUnchanged(
                    editorBeforeError,
                    GetBoundsRelativeTo(messageEditor, panel),
                    "Showing a property validation message must not move its editor.");
                AssertPositionUnchanged(
                    followingBeforeError,
                    GetBoundsRelativeTo(followingLabel, panel),
                    "Showing a property validation message must not move following fields.");

                var modeSelector = FindChildren<ComboBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Message:Mode",
                        StringComparison.Ordinal));
                modeSelector.SelectedIndex = 1;
                ArrangeAtPropertyPanelMinimum(panel);
                followingLabel = FindChildren<TextBlock>(panel)
                    .First(x => string.Equals(x.Text, "Enabled (Enabled)", StringComparison.Ordinal));
                var followingBeforeVariableSelection = GetBoundsRelativeTo(followingLabel, panel);
                var variableSelector = FindChildren<VariableSelectorControl>(panel).First();
                variableSelector.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, variableSelector));
                var sourceGroup = variableSelector.ContextMenu.Items
                    .OfType<MenuItem>()
                    .First(x => x.Items.Count > 0);
                var sourceItem = sourceGroup.Items.OfType<MenuItem>().First();
                sourceItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, sourceItem));
                ArrangeAtPropertyPanelMinimum(panel);
                AssertPositionUnchanged(
                    followingBeforeVariableSelection,
                    GetBoundsRelativeTo(followingLabel, panel),
                    "Clearing the variable validation status must not move following fields.");

                var policyPanel = new NodeExecutionPolicyPanelControl();
                policyPanel.ShowPolicy(node, descriptor, delegate { }, false);
                policyPanel.Measure(new Size(332, 1000));
                policyPanel.Arrange(new Rect(0, 0, 332, policyPanel.DesiredSize.Height));
                policyPanel.UpdateLayout();
                var timeoutEditor = FindChildren<TextBox>(policyPanel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.TimeoutMs",
                        StringComparison.Ordinal));
                var concurrencyLabel = FindChildren<TextBlock>(policyPanel)
                    .First(x => (x.Text ?? string.Empty).IndexOf("最大并发执行数", StringComparison.Ordinal) >= 0);
                var timeoutBeforeError = GetBoundsRelativeTo(timeoutEditor, policyPanel);
                var concurrencyBeforeError = GetBoundsRelativeTo(concurrencyLabel, policyPanel);

                timeoutEditor.Text = "invalid";
                policyPanel.Measure(new Size(332, 1000));
                policyPanel.Arrange(new Rect(0, 0, 332, policyPanel.DesiredSize.Height));
                policyPanel.UpdateLayout();
                AssertPositionUnchanged(
                    timeoutBeforeError,
                    GetBoundsRelativeTo(timeoutEditor, policyPanel),
                    "Showing an execution-policy error must not move its editor.");
                AssertPositionUnchanged(
                    concurrencyBeforeError,
                    GetBoundsRelativeTo(concurrencyLabel, policyPanel),
                    "Showing an execution-policy error must not move the following policy field.");
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

        public static Task EmbeddedToolbarHidesStandaloneDocumentCommands()
        {
            RunOnSta(delegate
            {
                var defaultOptions = new FlowDesignerOptions { LoadSampleOnStartup = false };
                AssertEx.True(defaultOptions.ShowStandaloneDocumentCommands,
                    "Standalone document commands should remain enabled by default for compatibility.");

                var standalone = new FlowDesignerControl(null, defaultOptions);
                var standaloneLabels = FindChildren<Button>(standalone)
                    .Select(GetButtonLabel)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                AssertEx.True(new[] { "New", "Sample", "Open", "Save", "Publish" }.All(standaloneLabels.Contains),
                    "Default designer toolbar should keep all standalone document commands.");

                var embedded = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                var embeddedLabels = FindChildren<Button>(embedded)
                    .Select(GetButtonLabel)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                AssertEx.False(new[] { "New", "Sample", "Open", "Save", "Publish" }.Any(embeddedLabels.Contains),
                    "Embedded designer toolbar should hide standalone document commands.");
                AssertEx.False(new[] { "编辑模式", "调试运行", "运行", "停止" }.Any(embeddedLabels.Contains),
                    "Embedded designer toolbar should not expose removed debug commands.");
            });
            return Task.FromResult(0);
        }

        public static Task ModernThemeAndExternalToolbarAreSelfContained()
        {
            RunOnSta(delegate
            {
                var theme = FlowDesignerTheme.CreateModern();
                AssertEx.NotNull(theme[FlowDesignerTheme.PageBackgroundBrushKey],
                    "Modern theme should expose the page background brush.");
                AssertEx.True(theme[FlowDesignerTheme.FieldTextBoxStyleKey] is Style,
                    "Modern theme should expose the 40 px field editor style.");
                AssertEx.True(theme[FlowDesignerTheme.ExpanderStyleKey] is Style,
                    "Modern theme should expose a vector-chevron expander style.");
                AssertEx.True(theme[FlowDesignerTheme.SegmentButtonStyleKey] is Style,
                    "Modern theme should expose the shared fixed/variable segment style.");
                AssertEx.True(theme[FlowDesignerTheme.CardBorderStyleKey] is Style,
                    "Modern theme should expose the shared card border style.");
                var cardStyle = (Style)theme[FlowDesignerTheme.CardBorderStyleKey];
                AssertEx.False(cardStyle.Setters.OfType<Setter>().Any(x => x.Property == FrameworkElement.MarginProperty),
                    "Shared card styling should not impose outer spacing on its consumers.");
                AssertEx.True(theme[FlowDesignerTheme.ErrorTextStyleKey] is Style,
                    "Modern theme should expose the shared inline error style.");
                AssertEx.True(theme[typeof(System.Windows.Controls.Primitives.ScrollBar)] is Style,
                    "Modern theme should include the shared compact scrollbar style.");
                var primaryStyle = (Style)theme[FlowDesignerTheme.PrimaryButtonStyleKey];
                AssertEx.True(primaryStyle.Setters.OfType<Setter>().Any(x => x.Property == Control.TemplateProperty),
                    "Primary buttons should own a green hover/pressed template instead of inheriting gray toolbar hover visuals.");

                var defaultOptions = new FlowDesignerOptions();
                AssertEx.Equal(FlowDesignerToolbarPlacement.Internal, defaultOptions.ToolbarPlacement,
                    "Internal toolbar placement should remain the compatibility default.");

                var external = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    ToolbarPlacement = FlowDesignerToolbarPlacement.External
                });
                AssertEx.NotNull(external.ToolbarView, "External placement should expose the command bar view.");
                var shellLabels = FindChildren<Button>(external)
                    .Select(GetButtonLabel)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                AssertEx.False(new[] { "编辑模式", "调试运行", "运行", "停止" }.Any(shellLabels.Contains),
                    "Removed debug commands must not appear in the designer shell.");
                AssertEx.NotNull(external.ToolbarView.TryFindResource(FlowDesignerTheme.ToolbarButtonStyleKey),
                    "External command bar should resolve its own theme without parent-resource inheritance.");

                var labels = FindChildren<Button>(external.ToolbarView)
                    .Select(GetButtonLabel)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                AssertEx.False(new[] { "编辑模式", "调试运行", "运行", "停止" }.Any(labels.Contains),
                    "External command bar should not expose removed debug commands.");

                external.ToolbarView.Measure(new Size(300, 50));
                external.ToolbarView.Arrange(new Rect(0, 0, 300, 50));
                external.ToolbarView.UpdateLayout();
                AssertEx.True(external.ToolbarView.DesiredSize.Width <= 300,
                    "External SDK commands should fit a strict 300 px host allocation without a status block.");
                AssertEx.True(LogicalTreeHelper.GetParent(external.ToolbarView) == null,
                    "External toolbar placement should leave ToolbarView available for exactly one host parent.");
                foreach (var button in FindChildren<Button>(external.ToolbarView).Where(x => x.Visibility == Visibility.Visible))
                {
                    var right = button.TranslatePoint(new Point(button.ActualWidth, 0), external.ToolbarView).X;
                    AssertEx.True(right <= 300.01,
                        "External command exceeded 300 px: " + GetButtonLabel(button) + " at " +
                        right.ToString(CultureInfo.InvariantCulture) + ".");
                    var icon = FindChildren<System.Windows.Shapes.Path>(button).FirstOrDefault();
                    if (icon != null)
                    {
                        AssertEx.NotNull(
                            System.Windows.Data.BindingOperations.GetBinding(icon, System.Windows.Shapes.Shape.StrokeProperty),
                            "Toolbar vector icon color should follow the owning button Foreground.");
                    }
                }

                var internalControl = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    ToolbarPlacement = FlowDesignerToolbarPlacement.Internal
                });
                AssertEx.NotNull(LogicalTreeHelper.GetParent(internalControl.ToolbarView),
                    "Internal toolbar placement should parent ToolbarView inside the designer shell.");
            });
            return Task.FromResult(0);
        }

        public static Task ListenerEntriesAndReadOnlyListFollowDocumentState()
        {
            RunOnSta(delegate
            {
                var registry = new NodeRegistry();
                CommonNodeRegistration.RegisterAll(registry);
                var listenerFactory = new TestListenerNodeFactory();
                registry.Register(listenerFactory);
                var control = new FlowDesignerControl(registry, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    ToolbarPlacement = FlowDesignerToolbarPlacement.External
                });
                var addNodeMethod = typeof(FlowDesignerControl).GetMethod(
                    "AddNodeFromPalette",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(NodeDescriptor) },
                    null);
                AssertEx.NotNull(addNodeMethod, "The palette add method should exist.");

                addNodeMethod.Invoke(control, new object[] { registry.GetFactory(DelayNodeFactory.TypeName).Descriptor });
                var document = GetPrivateField<FlowDesignDocument>(control, "_document");
                AssertEx.Equal(0, document.Runtime.Entries.Count,
                    "The first ordinary node must not become a manual entry because of insertion order.");

                addNodeMethod.Invoke(control, new object[] { listenerFactory.Descriptor });
                var listener = document.Runtime.Nodes.Last();
                AssertEx.Equal(1, document.Runtime.Entries.Count,
                    "Adding a listener should create exactly one NodeEvent entry.");
                AssertEx.Equal(FlowTriggerKind.NodeEvent, document.Runtime.Entries[0].TriggerKind,
                    "The automatic listener entry should use NodeEvent.");
                AssertEx.Equal(listener.Id, document.Runtime.Entries[0].SourceNodeId,
                    "The automatic listener entry should reference the added listener.");
                AssertEx.Equal("NodeEvent_" + listener.Id, document.Runtime.Entries[0].EntryName,
                    "The automatic listener entry should use the stable generated name.");

                InvokePrivate(control, "DuplicateNode", listener);
                var clone = document.Runtime.Nodes.Last();
                AssertEx.Equal(2, document.Runtime.Entries.Count,
                    "Duplicating a listener should create a separate entry.");
                AssertEx.True(document.Runtime.Entries.Select(x => x.EntryName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2,
                    "Duplicated listener entries should have unique protocol names.");
                InvokePrivate(control, "DeleteNode", clone);
                AssertEx.Equal(1, document.Runtime.Entries.Count,
                    "Deleting a listener should remove its NodeEvent entry.");

                var listContent = InvokePrivateInstance<FrameworkElement>(
                    control,
                    "CreateEntryListContent",
                    new Action(delegate { }));
                var texts = FindChildren<TextBlock>(listContent).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Contains("测试监听器") && texts.Contains("节点事件") && texts.Contains(listener.Id),
                    "The read-only list should show the node name, trigger kind and node id.");
                AssertEx.False(texts.Contains("入口名称") || texts.Contains("输入参数数量") || texts.Contains(document.Runtime.Entries[0].EntryName),
                    "The read-only list must hide EntryName and input counts.");
                var listButtons = FindChildren<Button>(listContent).ToList();
                AssertEx.Equal(1, listButtons.Count,
                    "The read-only list should expose only its close button.");
                AssertEx.Equal("EntryListClose", Convert.ToString(listButtons[0].Tag, CultureInfo.InvariantCulture),
                    "The only list action should be Close.");
                var toolbarLabels = FindChildren<Button>(control.ToolbarView)
                    .Select(GetButtonLabel)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();
                AssertEx.True(toolbarLabels.Contains("入口列表"),
                    "Both embedded and external toolbar hosts should expose the entry list command.");

                InvokePrivate(control, "DeleteNode", listener);
                var emptyContent = InvokePrivateInstance<FrameworkElement>(
                    control,
                    "CreateEntryListContent",
                    new Action(delegate { }));
                AssertEx.True(FindChildren<TextBlock>(emptyContent).Any(x => x.Text == "当前流程没有入口。"),
                    "The list should show an explicit empty state.");

                document.Runtime.Entries.Add(new FlowEntryDefinition
                {
                    EntryName = "MissingNode",
                    TriggerKind = FlowTriggerKind.External,
                    TargetNodeId = "missing-node"
                });
                var missingNodeContent = InvokePrivateInstance<FrameworkElement>(
                    control,
                    "CreateEntryListContent",
                    new Action(delegate { }));
                var missingNodeTexts = FindChildren<TextBlock>(missingNodeContent)
                    .Select(x => x.Text ?? string.Empty)
                    .ToList();
                AssertEx.True(missingNodeTexts.Contains("节点不存在") && missingNodeTexts.Contains("missing-node"),
                    "The list should retain an invalid node id and identify the missing node.");

                var staleDocument = new FlowDesignDocument
                {
                    FlowId = "stale-listener",
                    FlowName = "Stale Listener",
                    Runtime = new RuntimeFlowDefinition
                    {
                        FlowId = "stale-listener",
                        FlowName = "Stale Listener",
                        Version = "1.0.0",
                        Nodes =
                        {
                            new NodeDefinition
                            {
                                Id = "listener-stale",
                                Type = TestListenerNodeFactory.TypeName,
                                Name = "旧监听节点",
                                Version = "1.0.0"
                            }
                        }
                    },
                    View = new FlowViewState()
                };
                control.LoadDocumentAsync(staleDocument).GetAwaiter().GetResult();
                AssertEx.Equal(0, GetPrivateField<FlowDesignDocument>(control, "_document").Runtime.Entries.Count,
                    "Loading an old document must not repair missing listener entries.");
            });
            return Task.FromResult(0);
        }

        public static Task PaletteSearchesAllDescriptorFieldsAndRestoresExpansion()
        {
            RunOnSta(delegate
            {
                var camera = CreateSearchDescriptor("device.camera", "工业相机", "采集灰度图像", "设备");
                var delay = CreateSearchDescriptor("flow.delay", "等待节点", "暂停后继续", "流程控制");
                var log = CreateSearchDescriptor("log.write", "记录消息", "写入运行日志", "诊断");
                var palette = new NodePaletteControl();
                palette.SetDescriptors(new[] { camera, delay, log });

                var deviceGroup = FindChildren<Expander>(palette)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodePaletteCategory:设备", StringComparison.Ordinal));
                deviceGroup.IsExpanded = false;

                AssertPaletteSearchResult(palette, "工业相机", camera.NodeType, "DisplayName");
                AssertPaletteSearchResult(palette, "灰度图像", camera.NodeType, "Description");
                AssertPaletteSearchResult(palette, "flow.delay", delay.NodeType, "NodeType");
                AssertPaletteSearchResult(palette, "诊断", log.NodeType, "Category");
                AssertEx.True(FindChildren<Expander>(palette).All(x => x.IsExpanded),
                    "Searching should automatically expand every matching category.");

                palette.SearchText = string.Empty;
                deviceGroup = FindChildren<Expander>(palette)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodePaletteCategory:设备", StringComparison.Ordinal));
                AssertEx.False(deviceGroup.IsExpanded,
                    "Clearing search should restore the category's pre-search collapsed state.");

                palette.SearchText = "   ";
                var placeholder = FindChildren<TextBlock>(palette)
                    .First(x => string.Equals(x.Text, "搜索节点", StringComparison.Ordinal));
                AssertEx.Equal(Visibility.Visible, placeholder.Visibility,
                    "Whitespace-only search should behave like an empty query and keep the placeholder visible.");
            });
            return Task.FromResult(0);
        }

        public static Task PortsStayOutsideCardsAndEdgesEndNaturally()
        {
            RunOnSta(delegate
            {
                var host = new Grid { Width = 120, Height = 80 };
                var port = new PortControl(new PortViewModel(new NodePortDescriptor
                {
                    Name = FlowPortNames.In,
                    Direction = FlowPortDirection.Input,
                    DataType = FlowDataType.Control
                }))
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(20, 12, 0, 0)
                };
                host.Children.Add(port);
                host.Measure(new Size(120, 80));
                host.Arrange(new Rect(0, 0, 120, 80));
                host.UpdateLayout();

                var handle = FindChildren<Border>(port)
                    .First(x => Math.Abs(x.Width - 4) < 0.01 && Math.Abs(x.Height - 16) < 0.01);
                AssertEx.Equal(2.0, handle.CornerRadius.TopLeft,
                    "Port handles should be rendered as short edge tabs.");
                AssertEx.Equal(0.0, handle.BorderThickness.Left,
                    "Edge tabs should use a solid blue fill without a circular outline.");
                var expectedAnchor = handle.TranslatePoint(new Point(2, 8), host);
                var actualAnchor = port.GetAnchorPoint(host);
                AssertEx.Equal(expectedAnchor.X, actualAnchor.X,
                    "Port anchor X should be the edge-tab center.");
                AssertEx.Equal(expectedAnchor.Y, actualAnchor.Y,
                    "Port anchor Y should be the edge-tab center.");

                var card = new NodeCardControl(new NodeViewModel(CreateNode(), CreateDescriptor()));
                card.Measure(new Size(220, 500));
                card.Arrange(new Rect(0, 0, 220, card.DesiredSize.Height));
                card.UpdateLayout();
                var inputAnchor = card.InputPortControls.First().GetAnchorPoint(card);
                var outputAnchor = card.OutputPortControls.First().GetAnchorPoint(card);
                AssertEx.True(inputAnchor.X < 0,
                    "Input tabs should sit outside the card's left border.");
                AssertEx.True(outputAnchor.X > card.ActualWidth,
                    "Output tabs should sit outside the card's right border.");

                var document = CreateTwoDelayDocument();
                document.Runtime.Edges.Clear();
                document.Runtime.Edges.Add(new EdgeDefinition
                {
                    FromNodeId = "delay_1",
                    FromPort = FlowPortNames.Next,
                    ToNodeId = "delay_2",
                    ToPort = FlowPortNames.In
                });
                var edgeLayer = new EdgeLayerControl();
                var end = new Point(480, 180);
                edgeLayer.Render(document, null, new Dictionary<string, Point>
                {
                    { "delay_1|Output|Next", new Point(250, 180) },
                    { "delay_2|Input|In", end }
                });
                var arrow = FindChildren<System.Windows.Shapes.Path>(edgeLayer)
                    .FirstOrDefault(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "FlowEdgeArrow", StringComparison.Ordinal));
                AssertEx.True(arrow == null,
                    "Dify-style control connections should not add a detached triangular arrow.");
                var visibleEdge = FindChildren<System.Windows.Shapes.Path>(edgeLayer)
                    .First(x => !x.IsHitTestVisible && x.Data is PathGeometry);
                var geometry = (PathGeometry)visibleEdge.Data;
                var segment = (BezierSegment)geometry.Figures[0].Segments[0];
                AssertEx.Equal(end.X, segment.Point3.X,
                    "The Bezier connection should end at the external input-tab center.");
                AssertEx.Equal(end.Y, segment.Point3.Y,
                    "The Bezier connection should end at the external input-tab center.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyDraftAppliesResetsAndResolvesDecisions()
        {
            RunOnSta(delegate
            {
                var decision = PendingPropertyChangesDecision.Cancel;
                var promptCount = 0;
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    PendingPropertyChangesPrompt = delegate
                    {
                        promptCount++;
                        return decision;
                    }
                });
                control.LoadDocumentAsync(CreateDelayDocument("draft-node", "已应用名称", 25))
                    .GetAwaiter().GetResult();

                var source = GetPrivateField<FlowDesignDocument>(control, "_document");
                var nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodeName", StringComparison.Ordinal));
                nameEditor.Text = "草稿名称";

                AssertEx.True(control.HasPendingPropertyChanges,
                    "Editing a property should create a pending draft.");
                AssertEx.Equal("已应用名称", source.Runtime.Nodes[0].Name,
                    "Typing in the property panel must not mutate the source node.");

                AssertEx.False(control.TryResolvePendingPropertyChanges(),
                    "Cancel should keep the current operation and draft in place.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "Cancel should preserve the pending draft.");
                AssertEx.Equal("已应用名称", source.Runtime.Nodes[0].Name,
                    "Cancel should preserve the last applied source state.");

                decision = PendingPropertyChangesDecision.Discard;
                AssertEx.True(control.TryResolvePendingPropertyChanges(),
                    "Discard should resolve the pending decision.");
                AssertEx.False(control.HasPendingPropertyChanges,
                    "Discard should restore the latest applied state.");
                AssertEx.Equal("已应用名称", source.Runtime.Nodes[0].Name,
                    "Discard must not write the draft to the source node.");

                nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodeName", StringComparison.Ordinal));
                nameEditor.Text = "一次提交名称";
                decision = PendingPropertyChangesDecision.Apply;
                AssertEx.True(control.TryResolvePendingPropertyChanges(),
                    "Apply should validate and commit the complete draft.");
                AssertEx.False(control.HasPendingPropertyChanges,
                    "A successful apply should establish a new clean baseline.");
                AssertEx.Equal("一次提交名称", source.Runtime.Nodes[0].Name,
                    "Apply should write the draft to the source node once.");

                var delayEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:" + FlowSettingNames.DelayMs,
                        StringComparison.Ordinal));
                delayEditor.Text = "45";
                var timeoutEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.TimeoutMs", StringComparison.Ordinal));
                timeoutEditor.Text = "500";
                var concurrencyEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.MaxConcurrentExecutions", StringComparison.Ordinal));
                concurrencyEditor.Text = "2";
                var retryToggle = FindChildren<CheckBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.RetryPolicy.Enabled", StringComparison.Ordinal));
                retryToggle.IsChecked = true;
                var maxRetriesEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.RetryPolicy.MaxRetries", StringComparison.Ordinal));
                maxRetriesEditor.Text = "4";
                var retryIntervalEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.RetryPolicy.RetryIntervalMs", StringComparison.Ordinal));
                retryIntervalEditor.Text = "750";
                var failureSelector = FindChildren<ComboBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "ExecutionPolicy.FailureStrategy", StringComparison.Ordinal));
                failureSelector.SelectedIndex = 2;
                var fallbackEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.DefaultOutputs." + FlowSettingNames.DelayMs,
                        StringComparison.Ordinal));
                fallbackEditor.Text = "99";

                AssertEx.Equal(25, source.Runtime.Nodes[0].Settings[FlowSettingNames.DelayMs].ConstantValue,
                    "Valid setting edits should remain isolated in the draft until Apply.");
                AssertEx.Equal(0, source.Runtime.Nodes[0].ExecutionPolicy.TimeoutMs,
                    "Valid execution-policy edits should remain isolated in the draft until Apply.");
                string applyError;
                AssertEx.True(control.TryApplyPendingPropertyChanges(out applyError),
                    "One Apply should commit settings, retry policy and default outputs together.");
                AssertEx.Equal(45, source.Runtime.Nodes[0].Settings[FlowSettingNames.DelayMs].ConstantValue,
                    "Apply should commit a valid typed setting.");
                AssertEx.Equal(500, source.Runtime.Nodes[0].ExecutionPolicy.TimeoutMs,
                    "Apply should commit TimeoutMs.");
                AssertEx.Equal(2, source.Runtime.Nodes[0].ExecutionPolicy.MaxConcurrentExecutions,
                    "Apply should commit MaxConcurrentExecutions.");
                AssertEx.True(source.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy.Enabled,
                    "Apply should commit retry enabled state.");
                AssertEx.Equal(4, source.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy.MaxRetries,
                    "Apply should commit MaxRetries.");
                AssertEx.Equal(750, source.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy.RetryIntervalMs,
                    "Apply should commit RetryIntervalMs.");
                AssertEx.Equal(FailureStrategy.DefaultOutputs, source.Runtime.Nodes[0].ExecutionPolicy.FailureStrategy,
                    "Apply should commit the failure strategy.");
                AssertEx.Equal(99, source.Runtime.Nodes[0].ExecutionPolicy.DefaultOutputs[FlowSettingNames.DelayMs],
                    "Apply should commit typed default outputs.");

                nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodeName", StringComparison.Ordinal));
                nameEditor.Text = "等待重置";
                var resetButton = FindChildren<Button>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "PropertyReset", StringComparison.Ordinal));
                resetButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, resetButton));
                nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodeName", StringComparison.Ordinal));
                AssertEx.Equal("一次提交名称", nameEditor.Text,
                    "Reset should restore the most recently applied baseline in the editor.");
                AssertEx.Equal("一次提交名称", source.Runtime.Nodes[0].Name,
                    "Reset should not alter the most recently applied source state.");
                AssertEx.False(control.HasPendingPropertyChanges,
                    "Reset should return the property panel to a clean state.");

                AssertEx.True(control.TryResolvePendingPropertyChanges(),
                    "A clean property panel should resolve without prompting.");
                AssertEx.Equal(3, promptCount,
                    "Only the three dirty decisions should invoke the host prompt.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyDraftPromptDoesNotCaptureReleasedNodeClick()
        {
            RunOnSta(delegate
            {
                var decision = PendingPropertyChangesDecision.Cancel;
                var promptCount = 0;
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    PendingPropertyChangesPrompt = delegate
                    {
                        promptCount++;
                        return decision;
                    }
                });
                control.LoadDocumentAsync(CreateTwoDelayDocument())
                    .GetAwaiter().GetResult();

                var document = GetPrivateField<FlowDesignDocument>(control, "_document");
                var first = document.Runtime.Nodes[0];
                var second = document.Runtime.Nodes[1];
                var nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "NodeName",
                        StringComparison.Ordinal));
                nameEditor.Text = "等待处理的名称";

                var secondCard = FindChildren<NodeCardControl>(control)
                    .First(x => object.ReferenceEquals(x.ViewModel.Node, second));
                var releasedClick = new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = secondCard
                };

                InvokePrivate(control, "OnNodeMouseDown", secondCard, releasedClick);

                AssertEx.Equal(first, GetPrivateField<NodeDefinition>(control, "_selectedNode"),
                    "Cancel should keep the original node selected.");
                AssertEx.True(GetPrivateField<NodeCardControl>(control, "_dragCard") == null,
                    "Closing the pending-property prompt must not capture the original released node click.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "Cancel should keep the original property draft editable.");

                decision = PendingPropertyChangesDecision.Apply;
                releasedClick = new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = secondCard
                };
                InvokePrivate(control, "OnNodeMouseDown", secondCard, releasedClick);

                AssertEx.Equal(second, GetPrivateField<NodeDefinition>(control, "_selectedNode"),
                    "Apply should commit the draft and complete node selection.");
                AssertEx.True(GetPrivateField<NodeCardControl>(control, "_dragCard") == null,
                    "Apply must not leave the old node click in a pending drag state.");
                AssertEx.Equal("等待处理的名称", first.Name,
                    "Apply should commit the previous node draft.");

                nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "NodeName",
                        StringComparison.Ordinal));
                nameEditor.Text = "第二个节点可继续编辑";
                AssertEx.True(control.HasPendingPropertyChanges,
                    "The selected node property editor should accept input immediately after the prompt closes.");
                AssertEx.Equal(2, promptCount,
                    "Editing the selected node after Apply must not reopen the pending-property prompt.");
            });
            return Task.FromResult(0);
        }

        public static Task CanvasMiniMapTracksViewportAndClampsNavigation()
        {
            RunOnSta(delegate
            {
                var document = CreateTwoDelayDocument();
                var miniMap = new FlowMiniMapControl
                {
                    Width = 196,
                    Height = 132
                };
                miniMap.UpdateView(document, 2400, 1600, 2.0, 600, 400, 800, 600);
                miniMap.Measure(new Size(196, 132));
                miniMap.Arrange(new Rect(0, 0, 196, 132));

                var viewport = InvokePrivateInstance<Rect>(miniMap, "GetLogicalViewport");
                AssertEx.Equal(300.0, viewport.X,
                    "Mini-map viewport X should convert the scaled scroll offset to logical canvas coordinates.");
                AssertEx.Equal(200.0, viewport.Y,
                    "Mini-map viewport Y should convert the scaled scroll offset to logical canvas coordinates.");
                AssertEx.Equal(400.0, viewport.Width,
                    "Mini-map viewport width should account for canvas zoom.");
                AssertEx.Equal(300.0, viewport.Height,
                    "Mini-map viewport height should account for canvas zoom.");

                var clamped = InvokePrivateStatic<Point>(
                    typeof(FlowMiniMapControl),
                    "CalculateViewportOrigin",
                    new Point(2390, 1590),
                    new Point(100, 75),
                    2400.0,
                    1600.0,
                    400.0,
                    300.0);
                AssertEx.Equal(2000.0, clamped.X,
                    "Dragging the mini-map viewport should clamp navigation at the canvas right edge.");
                AssertEx.Equal(1300.0, clamped.Y,
                    "Dragging the mini-map viewport should clamp navigation at the canvas bottom edge.");

                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                var hostedMiniMap = GetPrivateField<FlowMiniMapControl>(control, "_miniMap");
                AssertEx.Equal(HorizontalAlignment.Left, hostedMiniMap.HorizontalAlignment,
                    "The canvas mini-map should be hosted in the lower-left corner.");
                AssertEx.Equal(VerticalAlignment.Bottom, hostedMiniMap.VerticalAlignment,
                    "The canvas mini-map should be hosted in the lower-left corner.");
            });
            return Task.FromResult(0);
        }

        public static Task CanvasPanAlignsOffsetsToDevicePixels()
        {
            var aligned = InvokePrivateStatic<double>(
                typeof(FlowDesignerControl),
                "AlignToDevicePixel",
                10.3,
                1.25);
            AssertEx.Equal(10.4, aligned,
                "Canvas panning should align fractional offsets to the current DPI device-pixel grid.");

            var clamped = InvokePrivateStatic<double>(
                typeof(FlowDesignerControl),
                "AlignToDevicePixel",
                -4.0,
                1.25);
            AssertEx.Equal(0.0, clamped,
                "Canvas panning should not produce negative scroll offsets.");
            return Task.FromResult(0);
        }

        public static Task PropertyDraftDiscardRestoresInvalidBaselineOnce()
        {
            RunOnSta(delegate
            {
                var promptCount = 0;
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    PendingPropertyChangesPrompt = delegate
                    {
                        promptCount++;
                        return PendingPropertyChangesDecision.Discard;
                    },
                    SettingConstantOptionsProvider = delegate(NodeSettingDescriptor setting)
                    {
                        return string.Equals(
                            setting == null ? null : setting.Name,
                            FlowSettingNames.DelayMs,
                            StringComparison.OrdinalIgnoreCase)
                            ? new NodeSettingConstantOption[0]
                            : null;
                    }
                });
                control.LoadDocumentAsync(CreateDelayDocument("draft-node", "已应用名称", 25))
                    .GetAwaiter().GetResult();

                AssertEx.False(control.HasPendingPropertyChanges,
                    "A validation error already present in the loaded baseline is not an unapplied draft.");
                var nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "NodeName",
                        StringComparison.Ordinal));
                nameEditor.Text = "等待放弃的名称";
                AssertEx.True(control.HasPendingPropertyChanges,
                    "Editing a node with an invalid baseline should still create a pending draft.");

                AssertEx.True(control.TryResolvePendingPropertyChanges(),
                    "Discard should resolve the edited draft.");
                AssertEx.False(control.HasPendingPropertyChanges,
                    "Discard should restore the invalid baseline without creating another draft.");
                AssertEx.Equal("已应用名称", control.CaptureDocument().Runtime.Nodes[0].Name,
                    "Capture after Discard should preserve the applied baseline.");
                AssertEx.Equal(1, promptCount,
                    "Discard followed by Capture must request a decision exactly once.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyDraftRejectsInvalidTextAndSurvivesRefresh()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                control.LoadDocumentAsync(CreateDelayDocument("invalid-node", "延时", 30))
                    .GetAwaiter().GetResult();

                var source = GetPrivateField<FlowDesignDocument>(control, "_document");
                var delayEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:" + FlowSettingNames.DelayMs,
                        StringComparison.Ordinal));
                delayEditor.Text = "not-a-number";

                AssertEx.True(control.HasPendingPropertyChanges,
                    "An invalid raw value should still count as a pending change.");
                AssertEx.Equal(30, source.Runtime.Nodes[0].Settings[FlowSettingNames.DelayMs].ConstantValue,
                    "Invalid numeric text must not be silently converted or written to the source node.");

                var policyEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.TimeoutMs",
                        StringComparison.Ordinal));
                policyEditor.Text = "invalid-timeout";

                control.RefreshSelectedNodeProperties();
                delayEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:" + FlowSettingNames.DelayMs,
                        StringComparison.Ordinal));
                policyEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.TimeoutMs",
                        StringComparison.Ordinal));
                AssertEx.Equal("not-a-number", delayEditor.Text,
                    "Refreshing dynamic candidates should preserve invalid setting text.");
                AssertEx.Equal("invalid-timeout", policyEditor.Text,
                    "Refreshing the same node should preserve invalid execution-policy text.");
                AssertEx.NotNull(delayEditor.ToolTip,
                    "The refreshed setting editor should retain its inline validation error.");
                AssertEx.NotNull(policyEditor.ToolTip,
                    "The refreshed policy editor should retain its inline validation error.");

                string error;
                AssertEx.False(control.TryApplyPendingPropertyChanges(out error),
                    "Apply should be blocked while any editor contains invalid raw text.");
                AssertEx.True(!string.IsNullOrWhiteSpace(error),
                    "A rejected apply should return an actionable validation message.");
                AssertEx.Equal("Setting:" + FlowSettingNames.DelayMs, Convert.ToString(delayEditor.Tag, CultureInfo.InvariantCulture),
                    "The first invalid editor should keep a stable tag for error navigation.");
                AssertEx.Equal(30, source.Runtime.Nodes[0].Settings[FlowSettingNames.DelayMs].ConstantValue,
                    "A failed apply must leave the source document unchanged.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyDraftApplyButtonTracksValidationState()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                control.LoadDocumentAsync(CreateDelayDocument("apply-state-node", "延时", 30))
                    .GetAwaiter().GetResult();

                var applyButton = FindChildren<Button>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "PropertyApply",
                        StringComparison.Ordinal));
                var timeoutEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.TimeoutMs",
                        StringComparison.Ordinal));

                AssertEx.False(applyButton.IsEnabled,
                    "Apply should be disabled while the selected node draft is clean.");

                timeoutEditor.Text = "abc";
                AssertEx.True(control.HasPendingPropertyChanges,
                    "Invalid execution-policy text should count as a pending property change.");
                AssertEx.False(applyButton.IsEnabled,
                    "Apply should be disabled immediately while an execution-policy editor has an error.");

                timeoutEditor.Text = "250";
                AssertEx.True(applyButton.IsEnabled,
                    "Apply should be re-enabled immediately after the editor becomes valid and the draft remains dirty.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyPanelLayoutKeepsFieldsAndFooterSeparated()
        {
            RunOnSta(delegate
            {
                var panel = new PropertyPanelControl();
                var node = CreateNode();
                var descriptor = CreateDescriptor();
                panel.ShowNode(node, descriptor, new[]
                {
                    new VariableSelectionOption(
                        VariableSelector.ForNodeOutput("source", "Image"),
                        "Source [source]",
                        "Source",
                        "source",
                        "Image",
                        FlowDataType.String)
                }, delegate { }, false);
                panel.SetPendingState(true, false);

                ArrangeAtPropertyPanelMinimum(panel);

                var messageLabel = FindChildren<TextBlock>(panel)
                    .First(x => string.Equals(x.Text, "Message (Message)", StringComparison.Ordinal));
                var messageEditor = FindChildren<TextBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:Message",
                        StringComparison.Ordinal));
                var constantSegment = FindChildren<Button>(panel)
                    .First(x => string.Equals(Convert.ToString(x.Content, CultureInfo.InvariantCulture), "固定值", StringComparison.Ordinal));
                var segmentGroup = LogicalTreeHelper.GetParent(constantSegment) as Grid;
                AssertEx.NotNull(segmentGroup,
                    "The fixed/variable controls should remain grouped in one segment row.");

                AssertVerticalSeparation(panel, messageLabel, segmentGroup, 2,
                    "The setting label must not overlap or stick to its segmented mode control.");
                AssertHorizontalSeparation(panel, segmentGroup, messageEditor, 6,
                    "The segmented mode control must remain separated from its value editor.");

                var scrollViewer = GetPrivateField<ScrollViewer>(panel, "_scrollViewer");
                var applyButton = GetPrivateField<Button>(panel, "_applyButton");
                var resetButton = GetPrivateField<Button>(panel, "_resetButton");
                var footer = FindAncestor<Border>(applyButton);
                AssertEx.NotNull(footer, "The property actions should remain inside the footer chrome.");
                AssertVerticalSeparation(panel, scrollViewer, footer, 0,
                    "The fixed property footer must not cover the scrollable form viewport.");
                AssertHorizontalSeparation(panel, resetButton, applyButton, 7,
                    "Reset and Apply must retain their intended spacing.");
                AssertElementIntersectsViewport(panel, applyButton, panel,
                    "The Apply action should remain visible at 380 x 680.");

                var timeoutLabel = FindChildren<TextBlock>(panel)
                    .First(x => (x.Text ?? string.Empty).IndexOf("单次超时", StringComparison.Ordinal) >= 0);
                var timeoutEditor = FindChildren<TextBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.TimeoutMs",
                        StringComparison.Ordinal));
                timeoutEditor.Text = "abc";
                ArrangeAtPropertyPanelMinimum(panel);

                var inlineError = FindChildren<TextBlock>(panel)
                    .First(x =>
                        x.Visibility == Visibility.Visible &&
                        (x.Text ?? string.Empty).IndexOf("不小于 0", StringComparison.Ordinal) >= 0);
                var timeoutHelp = FindChildren<TextBlock>(panel)
                    .First(x => (x.Text ?? string.Empty).IndexOf("继承流程全局超时", StringComparison.Ordinal) >= 0);
                inlineError.BringIntoView();
                panel.UpdateLayout();

                AssertVerticalSeparation(panel, timeoutLabel, timeoutEditor, 2,
                    "The execution-policy label and editor must not overlap after validation.");
                AssertVerticalSeparation(panel, timeoutEditor, inlineError, 2,
                    "The invalid editor and its inline error must not overlap.");
                AssertVerticalSeparation(panel, inlineError, timeoutHelp, 2,
                    "The inline error must not overlap the following help text.");
                AssertElementIntersectsViewport(panel, inlineError, scrollViewer,
                    "The inline error should remain visible inside the scroll viewport.");
                AssertVerticalSeparation(panel, scrollViewer, footer, 0,
                    "Growing inline validation content must not move beneath the fixed footer.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyPanelRequiredErrorFitsAboveFooterAtMinimumSize()
        {
            RunOnSta(delegate
            {
                var panel = new PropertyPanelControl();
                var node = CreateNode();
                var descriptor = CreateDescriptor();
                for (var index = 1; index <= 4; index++)
                {
                    var name = "CameraOption" + index.ToString(CultureInfo.InvariantCulture);
                    node.Settings[name] = NodeSettingValue.ForConstant("Value " + index.ToString(CultureInfo.InvariantCulture));
                    descriptor.Settings.Add(new NodeSettingDescriptor
                    {
                        Name = name,
                        DisplayName = "相机选项 " + index.ToString(CultureInfo.InvariantCulture),
                        DataType = FlowDataType.String
                    });
                }
                node.Settings["ParameterName"] = NodeSettingValue.ForConstant("ExposureTime");
                descriptor.Settings.Add(new NodeSettingDescriptor
                {
                    Name = "ParameterName",
                    DisplayName = "参数名称",
                    DataType = FlowDataType.String,
                    IsRequired = true
                });
                panel.ShowNode(node, descriptor, null, delegate
                {
                    panel.SetPendingState(true, false);
                }, false);
                panel.ApplyRequested += delegate
                {
                    string error;
                    if (!panel.TryValidate(out error))
                    {
                        panel.ShowValidationError(error);
                    }
                };

                ArrangeAtPropertyPanelMinimum(panel);
                var parameterEditor = FindChildren<TextBox>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:ParameterName",
                        StringComparison.Ordinal));
                parameterEditor.Text = "x";
                parameterEditor.Text = string.Empty;
                var applyButton = GetPrivateField<Button>(panel, "_applyButton");
                AssertEx.False(applyButton.IsEnabled,
                    "A live required-value error should disable Apply before validation runs.");

                string validationError;
                AssertEx.False(panel.TryValidate(out validationError),
                    "Host-initiated Apply resolution should reject the required-value error.");
                panel.ShowValidationError(validationError);
                ArrangeAtPropertyPanelMinimum(panel);

                var summary = FindChildren<TextBlock>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "PropertyValidationSummary",
                        StringComparison.Ordinal));
                var inlineError = FindChildren<TextBlock>(panel)
                    .First(x =>
                        !object.ReferenceEquals(x, summary) &&
                        x.Visibility == Visibility.Visible &&
                        (x.Text ?? string.Empty).IndexOf("参数名称", StringComparison.Ordinal) >= 0);
                var scrollViewer = GetPrivateField<ScrollViewer>(panel, "_scrollViewer");
                var footer = FindAncestor<Border>(applyButton);
                var actionRow = FindAncestor<DockPanel>(applyButton);

                AssertElementFullyInsideViewport(panel, inlineError, scrollViewer,
                    "The required-field error must remain fully visible after Apply shows the global summary.");
                AssertRenderedAtDesiredHeight(inlineError,
                    "The required-field error must not be vertically clipped.");
                AssertElementFullyInsideViewport(panel, summary, footer,
                    "The global validation summary must remain fully inside the fixed footer.");
                AssertRenderedAtDesiredHeight(summary,
                    "The global validation summary must not be vertically clipped.");
                AssertVerticalSeparation(panel, summary, actionRow, 6,
                    "The global validation summary must remain separated from the footer actions.");
                AssertVerticalSeparation(panel, scrollViewer, footer, 0,
                    "The fixed validation footer must not cover the scroll viewport.");

                var errorBrush = (SolidColorBrush)panel.FindResource(FlowDesignerTheme.ErrorBrushKey);
                var editorBrush = parameterEditor.BorderBrush as SolidColorBrush;
                AssertEx.NotNull(editorBrush,
                    "The invalid focused editor should expose a concrete validation border brush.");
                AssertEx.Equal(errorBrush.Color, editorBrush.Color,
                    "Validation red must take precedence over the focused field accent.");
                var validationOutline = FindChildren<Border>(panel)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ValidationOutline:Setting:ParameterName",
                        StringComparison.Ordinal));
                var outlineBrush = validationOutline.BorderBrush as SolidColorBrush;
                AssertEx.Equal(Visibility.Visible, validationOutline.Visibility,
                    "The invalid focused editor should render a validation outline above its focus chrome.");
                AssertEx.True(Panel.GetZIndex(validationOutline) > Panel.GetZIndex(parameterEditor),
                    "The validation outline must render above the editor focus chrome.");
                AssertEx.NotNull(outlineBrush,
                    "The validation outline should use the shared error brush.");
                AssertEx.Equal(errorBrush.Color, outlineBrush.Color,
                    "The topmost validation outline should remain red while the editor is focused.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyDraftGuardsLoad()
        {
            RunOnSta(delegate
            {
                var decision = PendingPropertyChangesDecision.Cancel;
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    PendingPropertyChangesPrompt = delegate { return decision; }
                });
                control.LoadDocumentAsync(CreateDelayDocument("first-flow", "第一个节点", 10)).GetAwaiter().GetResult();
                var nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodeName", StringComparison.Ordinal));
                nameEditor.Text = "尚未应用";

                control.LoadDocumentAsync(CreateDelayDocument("second-flow", "第二个节点", 20)).GetAwaiter().GetResult();
                var source = GetPrivateField<FlowDesignDocument>(control, "_document");
                AssertEx.Equal("first-flow", source.FlowId,
                    "Canceling the pending decision should keep the current document loaded.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "Canceling load should preserve the current property draft.");

                decision = PendingPropertyChangesDecision.Apply;
                control.LoadDocumentAsync(CreateDelayDocument("second-flow", "第二个节点", 20)).GetAwaiter().GetResult();
                source = GetPrivateField<FlowDesignDocument>(control, "_document");
                AssertEx.Equal("second-flow", source.FlowId,
                    "Applying the pending draft should allow the next document to load.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyDraftPreservesInvalidDynamicCandidates()
        {
            RunOnSta(delegate
            {
                var candidates = new List<string> { "30", "45" };
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    SettingConstantOptionsProvider = delegate(NodeSettingDescriptor setting)
                    {
                        return string.Equals(setting.Name, FlowSettingNames.DelayMs, StringComparison.Ordinal)
                            ? candidates
                                .Select(x => new NodeSettingConstantOption(x, x))
                                .ToArray()
                            : null;
                    }
                });
                control.LoadDocumentAsync(CreateDelayDocument("dynamic-flow", "动态候选", 30))
                    .GetAwaiter().GetResult();
                var source = GetPrivateField<FlowDesignDocument>(control, "_document");

                candidates.Clear();
                candidates.Add("45");
                control.RefreshSelectedNodeProperties();

                var editor = FindChildren<ComboBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:" + FlowSettingNames.DelayMs,
                        StringComparison.Ordinal));
                AssertEx.Equal("30", editor.Text,
                    "Refreshing candidates should preserve the applied value even when it is no longer available.");
                AssertEx.NotNull(editor.ToolTip,
                    "An unavailable dynamic candidate should show an inline error immediately.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "A candidate that becomes invalid should block destructive navigation until resolved.");

                string error;
                AssertEx.False(control.TryApplyPendingPropertyChanges(out error),
                    "Apply should reject a fixed value removed from the dynamic candidate list.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "A failed candidate validation should keep the draft and error intact.");
                AssertEx.Equal(30, source.Runtime.Nodes[0].Settings[FlowSettingNames.DelayMs].ConstantValue,
                    "Failed dynamic-candidate validation must not clear or overwrite the source value.");
            });
            return Task.FromResult(0);
        }

        public static Task DynamicDescriptorDraftRefreshesAndReconcilesFields()
        {
            RunOnSta(delegate
            {
                var registry = new NodeRegistry();
                CommonNodeRegistration.RegisterAll(registry);
                var dynamicFactory = new DesignerDynamicDescriptorFactory();
                registry.Register(dynamicFactory);
                var control = new FlowDesignerControl(registry, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    SettingConstantOptionsProvider = delegate(NodeSettingDescriptor setting)
                    {
                        return string.Equals(
                            setting == null ? null : setting.Name,
                            DesignerDynamicDescriptorFactory.CommandSetting,
                            StringComparison.OrdinalIgnoreCase)
                            ? new[]
                            {
                                new NodeSettingConstantOption("Alpha", "Alpha"),
                                new NodeSettingConstantOption("Beta", "Beta"),
                                new NodeSettingConstantOption("Broken", "Broken")
                            }
                            : null;
                    }
                });
                var addNodeMethod = typeof(FlowDesignerControl).GetMethod(
                    "AddNodeFromPalette",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(NodeDescriptor) },
                    null);
                AssertEx.NotNull(addNodeMethod, "The palette add method should exist.");
                addNodeMethod.Invoke(control, new object[] { dynamicFactory.Descriptor });
                var createdDocument = GetPrivateField<FlowDesignDocument>(control, "_document");
                var createdNode = createdDocument.Runtime.Nodes.Single();
                AssertEx.Equal(
                    7,
                    createdNode.Settings["AlphaInput"].ConstantValue,
                    "A newly added dynamic node should receive its default instance settings.");
                AssertEx.Equal(
                    "common-default",
                    createdNode.Settings["CommonInput"].ConstantValue,
                    "A palette descriptor may omit non-shaping settings without creating an invalid node.");

                control.LoadDocumentAsync(CreateDynamicDescriptorDocument())
                    .GetAwaiter().GetResult();

                var source = GetPrivateField<FlowDesignDocument>(control, "_document");
                var alphaEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:AlphaInput",
                        StringComparison.Ordinal));
                alphaEditor.Text = "invalid-alpha";
                var alphaDefaultOutputEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.DefaultOutputs.AlphaResult",
                        StringComparison.Ordinal));
                alphaDefaultOutputEditor.Text = "invalid-alpha-output";
                var timeoutEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.TimeoutMs",
                        StringComparison.Ordinal));
                timeoutEditor.Text = "invalid-timeout";

                var commandSelector = FindChildren<ComboBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:" + DesignerDynamicDescriptorFactory.CommandSetting,
                        StringComparison.Ordinal));
                commandSelector.SelectedIndex = 1;
                commandSelector.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, commandSelector));

                var draft = GetPrivateField<NodeDefinition>(control, "_propertyDraftNode");
                AssertEx.Equal("Beta", draft.Settings[DesignerDynamicDescriptorFactory.CommandSetting].ConstantValue,
                    "Changing an AffectsDescriptor setting should update the property draft.");
                AssertEx.False(draft.Settings.ContainsKey("AlphaInput"),
                    "The old command-specific setting should be removed from the draft.");
                AssertEx.Equal(9, draft.Settings["BetaInput"].ConstantValue,
                    "The new command-specific setting should be initialized from its descriptor default.");
                AssertEx.Equal(NodeSettingValueMode.Variable, draft.Settings["CommonInput"].Mode,
                    "A setting with an unchanged contract should retain variable mode.");
                AssertEx.Equal(VariableSelectorScope.TriggerInput, draft.Settings["CommonInput"].Selector.Scope,
                    "A setting with an unchanged contract should retain selector scope.");
                AssertEx.Equal(
                    "SharedText",
                    string.Join(".", draft.Settings["CommonInput"].Selector.Path),
                    "A setting with an unchanged contract should retain selector path.");
                AssertEx.Equal("preserved", draft.Settings["CommonInput"].ConstantValue,
                    "A setting with an unchanged contract should retain its fallback constant.");
                AssertEx.False(draft.ExecutionPolicy.DefaultOutputs.ContainsKey("AlphaResult"),
                    "A removed output should be pruned from DefaultOutputs.");
                AssertEx.Equal(0, draft.ExecutionPolicy.DefaultOutputs["BetaResult"],
                    "A new output should receive a typed fallback value when DefaultOutputs is active.");
                AssertEx.Equal("common-fallback", draft.ExecutionPolicy.DefaultOutputs["CommonResult"],
                    "A fallback output with an unchanged contract should be preserved.");
                AssertEx.False(FindChildren<FrameworkElement>(control).Any(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:AlphaInput",
                        StringComparison.Ordinal)),
                    "The removed setting editor should disappear immediately.");
                AssertEx.True(FindChildren<TextBox>(control).Any(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:BetaInput",
                        StringComparison.Ordinal)),
                    "The new setting editor should appear immediately without applying the draft.");

                timeoutEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "ExecutionPolicy.TimeoutMs",
                        StringComparison.Ordinal));
                AssertEx.Equal("invalid-timeout", timeoutEditor.Text,
                    "Refreshing a dynamic descriptor should preserve unrelated raw editor text.");
                var propertyPanel = GetPrivateField<PropertyPanelControl>(control, "_properties");
                var editorErrors = GetPrivateField<Dictionary<string, string>>(propertyPanel, "_editorErrors");
                AssertEx.False(editorErrors.ContainsKey("Setting:AlphaInput"),
                    "Refreshing should clear validation state only for removed settings.");
                AssertEx.True(propertyPanel.HasEditorErrors,
                    "An unrelated execution-policy error should remain active after descriptor refresh.");

                AssertEx.Equal(
                    "Alpha",
                    source.Runtime.Nodes[0].Settings[DesignerDynamicDescriptorFactory.CommandSetting].ConstantValue,
                    "A dynamic descriptor refresh must not mutate the applied source node.");
                AssertEx.Equal(
                    "AlphaResult",
                    source.Runtime.Nodes[1].Settings[FlowSettingNames.DelayMs].Selector.Path[1],
                    "A downstream selector for a removed output should remain intact for validation.");

                timeoutEditor.Text = "0";
                string applyError;
                AssertEx.True(control.TryApplyPendingPropertyChanges(out applyError),
                    "The dynamic draft should apply after unrelated input errors are corrected. Error: " + applyError);
                AssertEx.Equal(
                    "Beta",
                    source.Runtime.Nodes[0].Settings[DesignerDynamicDescriptorFactory.CommandSetting].ConstantValue,
                    "Applying should commit the selected dynamic descriptor state.");
                AssertEx.Equal(
                    "AlphaResult",
                    source.Runtime.Nodes[1].Settings[FlowSettingNames.DelayMs].Selector.Path[1],
                    "Applying an upstream shape change must not silently rewrite downstream selectors.");

                var cards = GetPrivateField<Dictionary<string, NodeCardControl>>(control, "_nodeCards");
                AssertEx.True(cards["dynamic_1"].ViewModel.Descriptor.Outputs.Any(x => x.Name == "BetaResult"),
                    "The node card should use the applied instance descriptor.");
                AssertEx.False(cards["dynamic_1"].ViewModel.Descriptor.Outputs.Any(x => x.Name == "AlphaResult"),
                    "The node card should stop showing outputs removed by the instance descriptor.");

                var suggestions = InvokePrivateInstance<IList<VariableSelectionOption>>(
                    control,
                    "CreateVariableSuggestions",
                    source.Runtime.Nodes[1]);
                AssertEx.SequenceEqual(
                    new[] { "CaptureFrameId", "CreatedAt", "Metadata", "TokenId", "Values" },
                    suggestions
                        .Where(x => x.Selector.Scope == VariableSelectorScope.Token)
                        .Select(x => x.Selector.Path[0])
                        .ToArray(),
                    "Designer Token suggestions should expose only the fixed runtime context fields.");
                AssertEx.True(suggestions.Any(x =>
                        x.Selector.Scope == VariableSelectorScope.NodeOutput &&
                        x.Selector.Path.Count >= 2 &&
                        string.Equals(x.Selector.Path[0], "dynamic_1", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Selector.Path[1], "BetaResult", StringComparison.OrdinalIgnoreCase)),
                    "Variable suggestions should expose outputs from the source node's instance descriptor.");
                AssertEx.False(suggestions.Any(x =>
                        x.Selector.Scope == VariableSelectorScope.NodeOutput &&
                        x.Selector.Path.Count >= 2 &&
                        string.Equals(x.Selector.Path[0], "dynamic_1", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Selector.Path[1], "AlphaResult", StringComparison.OrdinalIgnoreCase)),
                    "Variable suggestions should not expose outputs removed from the instance descriptor.");

                commandSelector = FindChildren<ComboBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:" + DesignerDynamicDescriptorFactory.CommandSetting,
                        StringComparison.Ordinal));
                commandSelector.SelectedIndex = 2;
                commandSelector.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, commandSelector));
                AssertEx.False(FindChildren<FrameworkElement>(control).Any(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        "Setting:BetaInput",
                        StringComparison.Ordinal)),
                    "A descriptor-resolution error should immediately fall back to the static palette shape.");
                draft = GetPrivateField<NodeDefinition>(control, "_propertyDraftNode");
                AssertEx.Equal(
                    "Broken",
                    draft.Settings[DesignerDynamicDescriptorFactory.CommandSetting].ConstantValue,
                    "Static fallback should retain the invalid shaping value for validator diagnostics.");
            });
            return Task.FromResult(0);
        }

        public static Task PropertyDraftValidatesVariablesAndNodeSwitchDecisions()
        {
            RunOnSta(delegate
            {
                var decision = PendingPropertyChangesDecision.Cancel;
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false,
                    PendingPropertyChangesPrompt = delegate { return decision; }
                });
                control.LoadDocumentAsync(CreateTwoDelayDocument())
                    .GetAwaiter().GetResult();
                var source = GetPrivateField<FlowDesignDocument>(control, "_document");
                var first = source.Runtime.Nodes[0];
                var second = source.Runtime.Nodes[1];
                var nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodeName", StringComparison.Ordinal));
                nameEditor.Text = "第一个草稿";

                InvokePrivate(control, "SelectNode", second);
                AssertEx.Equal(first, GetPrivateField<NodeDefinition>(control, "_selectedNode"),
                    "Cancel should block switching away from a dirty node.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "Canceling node selection should preserve the current draft.");

                decision = PendingPropertyChangesDecision.Discard;
                InvokePrivate(control, "SelectNode", second);
                AssertEx.Equal(second, GetPrivateField<NodeDefinition>(control, "_selectedNode"),
                    "Discard should allow switching to the requested node.");
                AssertEx.Equal("第一个节点", first.Name,
                    "Discard during node switching should preserve the applied source state.");

                InvokePrivate(control, "SelectNode", first);
                nameEditor = FindChildren<TextBox>(control)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "NodeName", StringComparison.Ordinal));
                nameEditor.Text = "切换前应用";
                decision = PendingPropertyChangesDecision.Apply;
                InvokePrivate(control, "SelectNode", second);
                AssertEx.Equal(second, GetPrivateField<NodeDefinition>(control, "_selectedNode"),
                    "Apply should commit the previous node and complete selection.");
                AssertEx.Equal("切换前应用", first.Name,
                    "Apply during node switching should commit the complete previous draft.");

                InvokePrivate(control, "SelectNode", first);
                var modeSelector = FindChildren<ComboBox>(control)
                    .First(x => string.Equals(
                        Convert.ToString(x.Tag, CultureInfo.InvariantCulture),
                        FlowSettingNames.DelayMs + ":Mode",
                        StringComparison.Ordinal));
                modeSelector.SelectedIndex = 1;
                string error;
                AssertEx.False(control.TryApplyPendingPropertyChanges(out error),
                    "A variable setting without a source should block Apply.");
                AssertEx.True(error.IndexOf("变量", StringComparison.Ordinal) >= 0,
                    "Missing variable validation should return a variable-specific message.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "Missing-variable validation should retain the dirty draft.");

                var draft = GetPrivateField<NodeDefinition>(control, "_propertyDraftNode");
                draft.Settings[FlowSettingNames.DelayMs] = NodeSettingValue.ForVariable(
                    VariableSelector.ForNodeOutput("condition_1", FlowOutputNames.IsMatched),
                    10);
                InvokePrivate(control, "RenderProperties");
                AssertEx.False(control.TryApplyPendingPropertyChanges(out error),
                    "A variable source with an incompatible output type should block Apply.");
                AssertEx.True(error.IndexOf("类型", StringComparison.Ordinal) >= 0,
                    "Incompatible variable validation should identify the type mismatch.");
                AssertEx.True(control.HasPendingPropertyChanges,
                    "An incompatible selector should remain in the draft after failed Apply.");
                AssertEx.Equal(NodeSettingValueMode.Constant, first.Settings[FlowSettingNames.DelayMs].Mode,
                    "Failed variable validation must not mutate the source setting mode.");
            });
            return Task.FromResult(0);
        }

        public static Task HostDocumentApiLoadsCapturesAndDeepCopies()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                var source = CreateHostDocument();
                control.LoadDocumentAsync(source).GetAwaiter().GetResult();
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

        public static Task HostApiPublishesRuntimeFile()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "VisionFlowSdk.Tests",
                "designer-publish-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "host-flow" + FlowFileExtensions.FlowRuntime);
            Directory.CreateDirectory(directory);
            try
            {
                RunOnSta(delegate
                {
                    var control = new FlowDesignerControl(null, new FlowDesignerOptions
                    {
                        LoadSampleOnStartup = false,
                        ShowStandaloneDocumentCommands = false
                    });
                    var document = new FlowDesignDocument
                    {
                        FlowId = "host-publish",
                        FlowName = "Host Publish",
                        Runtime = new RuntimeFlowDefinition
                        {
                            FlowId = "host-publish",
                            FlowName = "Host Publish",
                            Version = "1.0.0"
                        },
                        View = new FlowViewState { Zoom = 1.4 }
                    };
                    document.Runtime.Nodes.Add(new NodeDefinition
                    {
                        Id = "delay1",
                        Type = DelayNodeFactory.TypeName,
                        Name = "Host Delay",
                        Version = "1.0.0",
                        Settings =
                        {
                            { FlowSettingNames.DelayMs, NodeSettingValue.ForConstant(0) }
                        }
                    });
                    document.Runtime.Entries.Add(new FlowEntryDefinition
                    {
                        EntryName = "ManualStart",
                        TargetNodeId = "delay1"
                    });
                    document.View.Nodes["delay1"] = new NodeViewState { X = 320, Y = 224 };

                    control.LoadDocumentAsync(document).GetAwaiter().GetResult();
                    var result = control.PublishRuntimeFile(path);

                    AssertEx.True(result.IsSuccess, "The embedded designer API should publish a valid runtime file.");
                    AssertEx.True(File.Exists(path), "The embedded designer API should create the requested file.");
                    var loaded = RuntimeFlowSerializer.Load(path);
                    AssertEx.Equal(FlowSchema.CurrentVersion, loaded.SchemaVersion,
                        "The embedded designer should publish the current schema.");
                    AssertEx.Equal("Host Delay", loaded.Nodes[0].Name,
                        "The runtime file should contain the captured designer document.");

                    result.Runtime.Nodes[0].Name = "Changed publication result";
                    AssertEx.Equal("Host Delay", control.CaptureDocument().Runtime.Nodes[0].Name,
                        "Changing the returned runtime snapshot should not mutate the designer document.");
                    AssertEx.Equal("Host Delay", RuntimeFlowSerializer.Load(path).Nodes[0].Name,
                        "Changing the returned runtime snapshot should not mutate the runtime file.");

                    var json = File.ReadAllText(path);
                    AssertEx.False(json.IndexOf("View", StringComparison.OrdinalIgnoreCase) >= 0,
                        "Designer publication must remove view state from the runtime artifact.");
                    AssertEx.False(json.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0,
                        "Designer publication must remove canvas zoom from the runtime artifact.");
                });
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory);
                }
            }

            return Task.FromResult(0);
        }

        public static Task HostResetCreatesEmptyDocument()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, new FlowDesignerOptions { LoadSampleOnStartup = true });

                control.ResetDocumentAsync("strategy-123", "策略连线图").GetAwaiter().GetResult();
                var captured = control.CaptureDocument();

                AssertEx.Equal("strategy-123", captured.FlowId, "Reset should preserve the requested design FlowId.");
                AssertEx.Equal("strategy-123", captured.Runtime.FlowId, "Reset should keep runtime and design FlowId aligned.");
                AssertEx.Equal("策略连线图", captured.FlowName, "Reset should preserve the requested design FlowName.");
                AssertEx.Equal("策略连线图", captured.Runtime.FlowName, "Reset should keep runtime and design FlowName aligned.");
                AssertEx.Equal(0, captured.Runtime.Nodes.Count, "Reset should not add sample nodes.");
                AssertEx.Equal(0, captured.Runtime.Edges.Count, "Reset should create no edges.");
                AssertEx.Equal(0, captured.Runtime.Entries.Count, "Reset should create no entries.");

                InvokePrivate(control, "LoadCoreBasicTemplate");
                var sample = control.CaptureDocument();
                var condition = sample.Runtime.Nodes.First(x => x.Id == "condition_1");
                AssertEx.Equal(NodeSettingValueMode.Variable, condition.Settings[FlowSettingNames.LeftValue].Mode,
                    "The built-in sample should store its condition source as a structured variable setting.");
                AssertEx.True(condition.Settings[FlowSettingNames.LeftValue].Selector.Path.SequenceEqual(new[] { "inspectionResult" }),
                    "The built-in sample should select the typed trigger input without a legacy expression string.");
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

        public static Task CanvasZoomKeepsViewportAnchorStable()
        {
            var offset = InvokePrivateStatic<double>(
                typeof(FlowDesignerControl),
                "CalculateZoomedOffset",
                200.0,
                300.0,
                1.0,
                1.5);

            AssertEx.Equal(450.0, offset, "Zoom should compensate the scroll offset around the viewport anchor.");
            AssertEx.Equal(
                (200.0 + 300.0) / 1.0,
                (offset + 300.0) / 1.5,
                "The logical canvas point below the mouse should stay unchanged after zooming.");
            return Task.FromResult(0);
        }

        public static Task NodeCardUsesSharpTextRenderingOptions()
        {
            RunOnSta(delegate
            {
                var card = new NodeCardControl(new NodeViewModel(CreateNode(), CreateDescriptor()));
                var cardBody = GetPrivateField<Border>(card, "_cardBody");
                var shadowHost = GetPrivateField<Border>(card, "_cardShadowHost");

                AssertEx.True(card.UseLayoutRounding, "Node cards should round layout pixels to reduce blurry text while zoomed out.");
                AssertEx.True(card.SnapsToDevicePixels, "Node cards should snap to device pixels while zoomed out.");
                AssertEx.Equal(TextRenderingMode.ClearType, TextOptions.GetTextRenderingMode(card), "Node cards should use ClearType text rendering.");
                AssertEx.Equal(TextHintingMode.Fixed, TextOptions.GetTextHintingMode(card),
                    "Node cards should use fixed screen-pixel text hinting.");
                AssertEx.True(cardBody.Effect == null,
                    "The card content must not be rasterized together with its shadow.");
                AssertEx.True(shadowHost.Effect is System.Windows.Media.Effects.DropShadowEffect,
                    "The shadow should render on an independent background layer.");
                AssertEx.True(shadowHost.CacheMode is BitmapCache,
                    "The independent shadow layer should be cached for smooth canvas interaction.");

                card.SetCanvasZoom(0.75);
                AssertEx.Equal(TextFormattingMode.Display, TextOptions.GetTextFormattingMode(card),
                    "Node cards should prioritize screen-pixel glyph metrics at 75% zoom.");

                card.SetCanvasZoom(1.0);
                AssertEx.Equal(TextFormattingMode.Ideal, TextOptions.GetTextFormattingMode(card),
                    "Node cards should retain scalable ideal glyph metrics outside the 75% range.");
            });
            return Task.FromResult(0);
        }

        public static Task CanvasInteractionCoalescesHighFrequencyUpdates()
        {
            RunOnSta(delegate
            {
                var control = new FlowDesignerControl(null, new FlowDesignerOptions
                {
                    LoadSampleOnStartup = false,
                    ShowStandaloneDocumentCommands = false
                });
                var scale = GetPrivateField<ScaleTransform>(control, "_canvasScale");

                InvokePrivate(control, "QueueCanvasZoom", 1.1, new Point(120, 80));
                InvokePrivate(control, "QueueCanvasZoom", 1.1, new Point(140, 90));
                AssertEx.Equal(1.0, scale.ScaleX,
                    "High-frequency wheel input should not synchronously re-layout the canvas for every event.");
                AssertEx.True(GetPrivateField<bool>(control, "_isCanvasFrameScheduled"),
                    "Queued canvas input should schedule one composition frame.");
                AssertEx.True(GetPrivateField<bool>(control, "_hasPendingCanvasZoom"),
                    "The latest zoom should remain pending until the composition frame.");
                AssertEx.True(Math.Abs(1.21 - GetPrivateField<double>(control, "_pendingCanvasZoom")) < 0.000001,
                    "Wheel deltas received in one frame should accumulate into one zoom target.");

                InvokePrivate(control, "QueueCanvasPan", 10.0, 20.0);
                InvokePrivate(control, "QueueCanvasPan", 30.0, 40.0);
                AssertEx.Equal(30.0, GetPrivateField<double>(control, "_pendingPanHorizontalOffset"),
                    "Pan input should keep only the newest horizontal offset for the frame.");
                AssertEx.Equal(40.0, GetPrivateField<double>(control, "_pendingPanVerticalOffset"),
                    "Pan input should keep only the newest vertical offset for the frame.");

                InvokePrivate(control, "OnCanvasInteractionFrame", null, EventArgs.Empty);
                AssertEx.True(Math.Abs(1.21 - scale.ScaleX) < 0.000001,
                    "The composition frame should apply the accumulated zoom once.");
                AssertEx.False(GetPrivateField<bool>(control, "_hasPendingCanvasZoom"),
                    "The applied zoom should leave no stale pending work.");
                AssertEx.False(GetPrivateField<bool>(control, "_hasPendingCanvasPan"),
                    "The applied pan should leave no stale pending work.");
            });
            return Task.FromResult(0);
        }

        public static Task PaletteAndNodeCardShowDescriptorDescription()
        {
            RunOnSta(delegate
            {
                var descriptor = DelayNodeDescriptor.Create();
                var palette = new NodePaletteControl();
                palette.SetDescriptors(new[] { descriptor });
                var paletteTexts = FindChildren<TextBlock>(palette).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(paletteTexts.Contains(descriptor.DisplayName), "Palette should show the localized node display name.");
                AssertEx.True(paletteTexts.Contains(descriptor.Description), "Palette should show the localized node description instead of the protocol node type.");

                var node = CreateNode();
                node.Name = descriptor.DisplayName;
                node.Type = descriptor.NodeType;
                var card = new NodeCardControl(new NodeViewModel(node, descriptor));
                var cardTexts = FindChildren<TextBlock>(card).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(cardTexts.Contains(descriptor.DisplayName), "Node card should show the localized node name.");
                AssertEx.True(cardTexts.Contains(descriptor.Description), "Node card should show the localized node description.");
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

        private static void InvokePrivate(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            AssertEx.NotNull(method, "Private method should exist: " + name);
            method.Invoke(instance, args ?? new object[0]);
        }

        private static Task InvokePrivateTask(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            AssertEx.NotNull(method, "Private async method should exist: " + name);
            return (Task)method.Invoke(instance, args ?? new object[0]);
        }

        private static T InvokePrivateStatic<T>(Type type, string name, params object[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            AssertEx.NotNull(method, "Private static method should exist: " + name);
            return (T)method.Invoke(null, args);
        }

        private static T InvokePrivateInstance<T>(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            AssertEx.NotNull(method, "Private method should exist: " + name);
            return (T)method.Invoke(instance, args ?? new object[0]);
        }

        private static string GetButtonLabel(Button button)
        {
            if (button == null)
            {
                return null;
            }

            var automationName = AutomationProperties.GetName(button);
            if (!string.IsNullOrWhiteSpace(automationName))
            {
                return automationName;
            }

            return button.Content as string;
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

        private static FlowDesignDocument CreateDelayDocument(string flowId, string nodeName, int delayMs)
        {
            var document = new FlowDesignDocument
            {
                FlowId = flowId,
                FlowName = flowId,
                Runtime = new RuntimeFlowDefinition
                {
                    FlowId = flowId,
                    FlowName = flowId,
                    Version = "1.0.0"
                },
                View = new FlowViewState()
            };
            document.Runtime.Nodes.Add(new NodeDefinition
            {
                Id = "delay_1",
                Type = DelayNodeFactory.TypeName,
                Name = nodeName,
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.DelayMs, NodeSettingValue.ForConstant(delayMs) }
                }
            });
            document.Runtime.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "delay_1"
            });
            document.View.Nodes["delay_1"] = new NodeViewState { X = 80, Y = 96 };
            return document;
        }

        private static FlowDesignDocument CreateTwoDelayDocument()
        {
            var document = CreateDelayDocument("two-delay-flow", "第一个节点", 10);
            document.Runtime.Nodes.Add(new NodeDefinition
            {
                Id = "delay_2",
                Type = DelayNodeFactory.TypeName,
                Name = "第二个节点",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.DelayMs, NodeSettingValue.ForConstant(20) }
                }
            });
            document.Runtime.Nodes.Add(new NodeDefinition
            {
                Id = "condition_1",
                Type = ConditionNodeFactory.TypeName,
                Name = "布尔来源",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.LeftValue, NodeSettingValue.ForVariable(VariableSelector.ForToken("TokenId")) },
                    { FlowSettingNames.Operator, NodeSettingValue.ForConstant("Equal") },
                    { FlowSettingNames.RightValue, NodeSettingValue.ForConstant("x") }
                }
            });
            document.Runtime.Entries[0].TargetNodeId = "condition_1";
            document.Runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "condition_1",
                FromPort = FlowPortNames.True,
                ToNodeId = "delay_1",
                ToPort = FlowPortNames.In
            });
            document.View.Nodes["delay_2"] = new NodeViewState { X = 560, Y = 224 };
            document.View.Nodes["condition_1"] = new NodeViewState { X = 80, Y = 96 };
            document.View.Nodes["delay_1"] = new NodeViewState { X = 320, Y = 96 };
            return document;
        }

        private static FlowDesignDocument CreateDynamicDescriptorDocument()
        {
            var document = new FlowDesignDocument
            {
                FlowId = "designer-dynamic-descriptor",
                FlowName = "Designer Dynamic Descriptor",
                Runtime = new RuntimeFlowDefinition
                {
                    FlowId = "designer-dynamic-descriptor",
                    FlowName = "Designer Dynamic Descriptor",
                    Version = "1.0.0"
                },
                View = new FlowViewState()
            };
            document.Runtime.Nodes.Add(new NodeDefinition
            {
                Id = "dynamic_1",
                Type = DesignerDynamicDescriptorFactory.TypeName,
                Name = "动态命令",
                Version = "1.0.0",
                Settings =
                {
                    {
                        DesignerDynamicDescriptorFactory.CommandSetting,
                        NodeSettingValue.ForConstant("Alpha")
                    },
                    {
                        "CommonInput",
                        NodeSettingValue.ForVariable(
                            VariableSelector.ForTriggerInput("SharedText"),
                            "preserved")
                    }
                },
                ExecutionPolicy = new NodeExecutionPolicy
                {
                    FailureStrategy = FailureStrategy.DefaultOutputs,
                    DefaultOutputs =
                    {
                        { "CommonResult", "common-fallback" }
                    }
                }
            });
            document.Runtime.Nodes.Add(new NodeDefinition
            {
                Id = "delay_after_dynamic",
                Type = DelayNodeFactory.TypeName,
                Name = "下游节点",
                Version = "1.0.0",
                Settings =
                {
                    {
                        FlowSettingNames.DelayMs,
                        NodeSettingValue.ForVariable(
                            VariableSelector.ForNodeOutput("dynamic_1", "AlphaResult"),
                            0)
                    }
                }
            });
            document.Runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "dynamic_1",
                FromPort = FlowPortNames.Next,
                ToNodeId = "delay_after_dynamic",
                ToPort = FlowPortNames.In
            });
            document.Runtime.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "dynamic_1",
                Inputs =
                {
                    new TriggerInputDescriptor
                    {
                        Name = "SharedText",
                        DisplayName = "共享文本",
                        DataType = FlowDataType.String,
                        IsRequired = true
                    }
                }
            });
            document.View.Nodes["dynamic_1"] = new NodeViewState { X = 80, Y = 96 };
            document.View.Nodes["delay_after_dynamic"] = new NodeViewState { X = 360, Y = 96 };
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
                    { "Message", NodeSettingValue.ForConstant("hello") },
                    { "Enabled", NodeSettingValue.ForConstant(true) }
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
                DataType = FlowDataType.String,
                BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                AllowedVariableSources = VariableSelectorScopeFlags.NodeOutput |
                    VariableSelectorScopeFlags.TriggerInput |
                    VariableSelectorScopeFlags.Token
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

        private static NodeDescriptor CreateSearchDescriptor(
            string nodeType,
            string displayName,
            string description,
            string category)
        {
            return new NodeDescriptor
            {
                NodeType = nodeType,
                DisplayName = displayName,
                Description = description,
                Category = category,
                Version = "1.0.0"
            };
        }

        private sealed class DesignerDynamicDescriptorFactory : INodeFactory, IInstanceNodeDescriptorProvider
        {
            public const string TypeName = "test.designer-dynamic-descriptor";
            public const string CommandSetting = "Command";

            private readonly NodeDescriptor _descriptor = CreatePaletteDescriptor();

            public string NodeType
            {
                get { return TypeName; }
            }

            public NodeDescriptor Descriptor
            {
                get { return _descriptor; }
            }

            public NodeDescriptor GetDescriptor(NodeDefinition definition)
            {
                NodeSettingValue value;
                var command = definition != null &&
                    definition.Settings != null &&
                    definition.Settings.TryGetValue(CommandSetting, out value) &&
                    value != null
                    ? Convert.ToString(value.ConstantValue, CultureInfo.InvariantCulture)
                    : "Alpha";
                if (string.Equals(command, "Broken", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Broken dynamic descriptor requested.");
                }

                return CreateDescriptor(string.Equals(command, "Beta", StringComparison.OrdinalIgnoreCase)
                    ? "Beta"
                    : "Alpha");
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return new RecordingNode(definition, new List<string>());
            }

            private static NodeDescriptor CreateDescriptor(string command)
            {
                var isBeta = string.Equals(command, "Beta", StringComparison.OrdinalIgnoreCase);
                var variant = isBeta ? "Beta" : "Alpha";
                var descriptor = new NodeDescriptor
                {
                    NodeType = TypeName,
                    DisplayName = "设计器动态描述符",
                    Category = "测试",
                    Version = "1.0.0",
                    InputPorts =
                    {
                        new NodePortDescriptor
                        {
                            Name = FlowPortNames.In,
                            DisplayName = FlowPortNames.In,
                            Direction = FlowPortDirection.Input,
                            DataType = FlowDataType.Control,
                            IsRequired = true
                        }
                    },
                    OutputPorts =
                    {
                        new NodePortDescriptor
                        {
                            Name = FlowPortNames.Next,
                            DisplayName = FlowPortNames.Next,
                            Direction = FlowPortDirection.Output,
                            DataType = FlowDataType.Control
                        }
                    },
                    Settings =
                    {
                        new NodeSettingDescriptor
                        {
                            Name = CommandSetting,
                            DisplayName = "命令",
                            DataType = FlowDataType.String,
                            DefaultValue = "Alpha",
                            IsRequired = true,
                            BindingMode = NodeSettingBindingMode.ConstantOnly,
                            EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                            AllowedVariableSources = VariableSelectorScopeFlags.None,
                            AffectsDescriptor = true
                        },
                        new NodeSettingDescriptor
                        {
                            Name = "CommonInput",
                            DisplayName = "公共输入",
                            DataType = FlowDataType.String,
                            DefaultValue = "common-default",
                            BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                            EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                            AllowedVariableSources = VariableSelectorScopeFlags.All
                        },
                        new NodeSettingDescriptor
                        {
                            Name = variant + "Input",
                            DisplayName = variant + " 输入",
                            DataType = FlowDataType.Int32,
                            DefaultValue = isBeta ? 9 : 7,
                            IsRequired = true,
                            BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                            EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                            AllowedVariableSources = VariableSelectorScopeFlags.All
                        }
                    },
                    Outputs =
                    {
                        new NodeOutputDescriptor
                        {
                            Name = "CommonResult",
                            DisplayName = "公共结果",
                            DataType = FlowDataType.String
                        },
                        new NodeOutputDescriptor
                        {
                            Name = variant + "Result",
                            DisplayName = variant + " 结果",
                            DataType = FlowDataType.Int32
                        }
                    }
                };
                return descriptor;
            }

            private static NodeDescriptor CreatePaletteDescriptor()
            {
                var descriptor = CreateDescriptor("Alpha");
                descriptor.Settings = descriptor.Settings
                    .Where(x => string.Equals(x.Name, CommandSetting, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                descriptor.Outputs.Clear();
                return descriptor;
            }
        }

        private static void AssertPaletteSearchResult(
            NodePaletteControl palette,
            string search,
            string expectedNodeType,
            string fieldName)
        {
            palette.SearchText = search;
            var matches = FindChildren<Button>(palette)
                .Select(x => x.Tag as NodeDescriptor)
                .Where(x => x != null)
                .Select(x => x.NodeType)
                .ToList();
            AssertEx.Equal(1, matches.Count,
                "Palette search should filter to one descriptor by " + fieldName + ".");
            AssertEx.Equal(expectedNodeType, matches[0],
                "Palette search should match descriptor " + fieldName + ".");
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

        private static void ArrangeAtPropertyPanelMinimum(PropertyPanelControl panel)
        {
            panel.Measure(new Size(380, 680));
            panel.Arrange(new Rect(0, 0, 380, 680));
            panel.UpdateLayout();
        }

        private static Rect GetBoundsRelativeTo(FrameworkElement element, Visual root)
        {
            var topLeft = element.TransformToAncestor(root).Transform(new Point(0, 0));
            return new Rect(topLeft, element.RenderSize);
        }

        private static void AssertVerticalSeparation(
            Visual root,
            FrameworkElement upper,
            FrameworkElement lower,
            double minimumGap,
            string message)
        {
            var upperBounds = GetBoundsRelativeTo(upper, root);
            var lowerBounds = GetBoundsRelativeTo(lower, root);
            AssertEx.True(
                upperBounds.Bottom + minimumGap <= lowerBounds.Top + 0.01,
                message + " Upper=" + upperBounds + ", Lower=" + lowerBounds + ".");
        }

        private static void AssertHorizontalSeparation(
            Visual root,
            FrameworkElement left,
            FrameworkElement right,
            double minimumGap,
            string message)
        {
            var leftBounds = GetBoundsRelativeTo(left, root);
            var rightBounds = GetBoundsRelativeTo(right, root);
            AssertEx.True(
                leftBounds.Right + minimumGap <= rightBounds.Left + 0.01,
                message + " Left=" + leftBounds + ", Right=" + rightBounds + ".");
        }

        private static void AssertPositionUnchanged(Rect expected, Rect actual, string message)
        {
            AssertEx.True(
                Math.Abs(expected.X - actual.X) < 0.01 &&
                Math.Abs(expected.Y - actual.Y) < 0.01,
                message + " Expected=" + expected + ", Actual=" + actual + ".");
        }

        private static void AssertTopAligned(
            Visual root,
            FrameworkElement first,
            FrameworkElement second,
            string message)
        {
            var firstBounds = GetBoundsRelativeTo(first, root);
            var secondBounds = GetBoundsRelativeTo(second, root);
            AssertEx.True(
                Math.Abs(firstBounds.Top - secondBounds.Top) < 0.01,
                message + " First=" + firstBounds + ", Second=" + secondBounds + ".");
        }

        private static void AssertSizeUnchanged(Rect expected, Rect actual, string message)
        {
            AssertEx.True(
                Math.Abs(expected.Width - actual.Width) < 0.01 &&
                Math.Abs(expected.Height - actual.Height) < 0.01,
                message + " Expected=" + expected + ", Actual=" + actual + ".");
        }

        private static void AssertElementIntersectsViewport(
            Visual root,
            FrameworkElement element,
            FrameworkElement viewport,
            string message)
        {
            var elementBounds = GetBoundsRelativeTo(element, root);
            var viewportBounds = GetBoundsRelativeTo(viewport, root);
            AssertEx.True(
                elementBounds.Left < viewportBounds.Right &&
                elementBounds.Right > viewportBounds.Left &&
                elementBounds.Top < viewportBounds.Bottom &&
                elementBounds.Bottom > viewportBounds.Top,
                message + " Element=" + elementBounds + ", Viewport=" + viewportBounds + ".");
        }

        private static void AssertElementFullyInsideViewport(
            Visual root,
            FrameworkElement element,
            FrameworkElement viewport,
            string message)
        {
            var elementBounds = GetBoundsRelativeTo(element, root);
            var viewportBounds = GetBoundsRelativeTo(viewport, root);
            AssertEx.True(
                elementBounds.Left >= viewportBounds.Left - 0.01 &&
                elementBounds.Right <= viewportBounds.Right + 0.01 &&
                elementBounds.Top >= viewportBounds.Top - 0.01 &&
                elementBounds.Bottom <= viewportBounds.Bottom + 0.01,
                message + " Element=" + elementBounds + ", Viewport=" + viewportBounds + ".");
        }

        private static void AssertRenderedAtDesiredHeight(FrameworkElement element, string message)
        {
            var desiredContentHeight = Math.Max(
                0,
                element.DesiredSize.Height - element.Margin.Top - element.Margin.Bottom);
            AssertEx.True(
                element.ActualHeight + 0.01 >= desiredContentHeight,
                message + " ActualHeight=" +
                element.ActualHeight.ToString(CultureInfo.InvariantCulture) +
                ", DesiredContentHeight=" +
                desiredContentHeight.ToString(CultureInfo.InvariantCulture) + ".");
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

        private sealed class DesignerTypedObjectPayload
        {
            public string Name { get; set; }

            public int Count = 1;

            public DesignerTypedNestedPayload Nested { get; set; }
        }

        private sealed class DesignerTypedNestedPayload
        {
            public string Code { get; set; }
        }
    }
}
