using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Vision.Flow.Nodes;

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
                        FlowDataType.Object)
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
                    FlowDataType.Object);
                editablePanel.ShowNode(editableNode, descriptor, new[] { compatibleOption }, delegate { }, false);

                var modeSelector = FindChildren<ComboBox>(editablePanel)
                    .FirstOrDefault(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "Message:Mode", StringComparison.Ordinal));
                AssertEx.NotNull(modeSelector, "A bindable setting should render an inline constant/variable mode selector.");
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

                var manualEntry = new FlowEntryDefinition
                {
                    EntryName = "ManualInspect",
                    TargetNodeId = "c",
                    TriggerKind = FlowTriggerKind.Manual,
                    Inputs =
                    {
                        new TriggerInputDescriptor
                        {
                            Name = "BatchSize",
                            DisplayName = "批次数量",
                            DataType = FlowDataType.Int32,
                            IsRequired = true
                        },
                        new TriggerInputDescriptor
                        {
                            Name = "Product",
                            DisplayName = "产品",
                            DataType = FlowDataType.String,
                            DefaultValue = "DemoProduct"
                        }
                    }
                };
                var externalEntry = new FlowEntryDefinition
                {
                    EntryName = "ExternalInspect",
                    TargetNodeId = "c",
                    TriggerKind = FlowTriggerKind.External,
                    Inputs =
                    {
                        new TriggerInputDescriptor
                        {
                            Name = "Payload",
                            DisplayName = "请求数据",
                            DataType = FlowDataType.Object,
                            IsRequired = true
                        }
                    }
                };
                var nodeEventEntry = new FlowEntryDefinition
                {
                    EntryName = "CameraFrame",
                    SourceNodeId = "camera_listener",
                    TargetNodeId = "c",
                    TriggerKind = FlowTriggerKind.NodeEvent
                };
                var triggerPanel = new EntryTriggerPanelControl();
                triggerPanel.ShowEntries(new[] { manualEntry, externalEntry, nodeEventEntry }, "ManualInspect", false);
                AssertEx.True(object.ReferenceEquals(manualEntry, triggerPanel.SelectedEntry),
                    "The trigger panel should restore the requested entry selection.");
                var batchEditor = FindChildren<TextBox>(triggerPanel)
                    .FirstOrDefault(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "BatchSize", StringComparison.Ordinal));
                AssertEx.NotNull(batchEditor, "A manual entry should generate editors from TriggerInputDescriptor.");
                batchEditor.Text = "12";
                FlowTriggerRequest triggerRequest;
                string triggerError;
                AssertEx.True(triggerPanel.TryCreateManualRequest(new FlowToken(), out triggerRequest, out triggerError),
                    "Valid manual input values should create a FlowTriggerRequest: " + triggerError);
                AssertEx.Equal(12, triggerRequest.Inputs["BatchSize"],
                    "The manual trigger form should convert input text to the descriptor data type.");
                AssertEx.Equal("DemoProduct", triggerRequest.Inputs["Product"],
                    "The manual trigger form should use descriptor defaults when the user leaves the value unchanged.");

                batchEditor.Text = string.Empty;
                AssertEx.False(triggerPanel.TryCreateManualRequest(new FlowToken(), out triggerRequest, out triggerError),
                    "A missing required manual input should block the debug trigger.");
                AssertEx.True((triggerError ?? string.Empty).IndexOf("BatchSize", StringComparison.OrdinalIgnoreCase) >= 0,
                    "The required-input error should identify the stable input name.");

                var entrySelector = FindChildren<ComboBox>(triggerPanel)
                    .FirstOrDefault(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "DebugEntrySelector", StringComparison.Ordinal));
                AssertEx.NotNull(entrySelector, "The trigger panel should provide an entry selector.");
                entrySelector.SelectedIndex = 1;
                AssertEx.Equal(FlowTriggerKind.External, triggerPanel.SelectedEntry.TriggerKind,
                    "The entry selector should allow inspecting an External entry.");
                AssertEx.True(FindChildren<TextBlock>(triggerPanel).Any(x => (x.Text ?? string.Empty).IndexOf("外部宿主", StringComparison.Ordinal) >= 0),
                    "External entries should show host-trigger information instead of manual editors.");
                AssertEx.False(triggerPanel.TryCreateManualRequest(new FlowToken(), out triggerRequest, out triggerError),
                    "External entries should not be manually triggered by the designer.");

                entrySelector = FindChildren<ComboBox>(triggerPanel)
                    .First(x => string.Equals(Convert.ToString(x.Tag, CultureInfo.InvariantCulture), "DebugEntrySelector", StringComparison.Ordinal));
                entrySelector.SelectedIndex = 2;
                AssertEx.True(FindChildren<TextBlock>(triggerPanel).Any(x => (x.Text ?? string.Empty).IndexOf("camera_listener", StringComparison.Ordinal) >= 0),
                    "NodeEvent entries should display their listener source node.");

                triggerPanel.ShowEntries(new[] { manualEntry, externalEntry, nodeEventEntry }, "ManualInspect", true);
                AssertEx.True(FindChildren<ComboBox>(triggerPanel).All(x => !x.IsEnabled),
                    "Entry selection should be disabled while a debug run is active.");
                AssertEx.True(FindChildren<TextBox>(triggerPanel).All(x => x.IsReadOnly),
                    "Manual trigger inputs should be read-only while a debug run is active.");

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
                var candidateControl = new FlowDesignerControl(null, null, new FlowDesignerOptions { LoadSampleOnStartup = false });
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
                control.LoadDocumentAsync(CreateHostDocument()).GetAwaiter().GetResult();
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
                    var control = new FlowDesignerControl(null, null, new FlowDesignerOptions
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

                InvokePrivate(control, "LoadCoreBasicTemplate");
                var sample = control.CaptureDocument();
                var condition = sample.Runtime.Nodes.First(x => x.Id == "condition_1");
                AssertEx.Equal(NodeSettingValueMode.Variable, condition.Settings[FlowSettingNames.LeftBinding].Mode,
                    "The built-in sample should store its condition source as a structured variable setting.");
                AssertEx.True(condition.Settings[FlowSettingNames.LeftBinding].Selector.Path.SequenceEqual(new[] { "set_result", "Value" }),
                    "The built-in sample should select set_result.Value without a legacy expression string.");
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

                AssertEx.True(card.UseLayoutRounding, "Node cards should round layout pixels to reduce blurry text while zoomed out.");
                AssertEx.True(card.SnapsToDevicePixels, "Node cards should snap to device pixels while zoomed out.");
                AssertEx.Equal(TextFormattingMode.Ideal, TextOptions.GetTextFormattingMode(card), "Node cards should use scalable ideal text formatting.");
                AssertEx.Equal(TextRenderingMode.ClearType, TextOptions.GetTextRenderingMode(card), "Node cards should use ClearType text rendering.");
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

                var retryNode = CreateNode();
                retryNode.ExecutionPolicy.RetryPolicy.Enabled = true;
                var eventCard = new NodeCardControl(new NodeViewModel(retryNode, CreateDescriptor()));
                var control = new FlowDesignerControl(null, null, new FlowDesignerOptions { LoadSampleOnStartup = false });
                SetDesignerMode(control, "DebugRun");
                GetPrivateField<Dictionary<string, NodeCardControl>>(control, "_nodeCards")[retryNode.Id] = eventCard;

                var retrying = new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.NodeRetrying,
                    NodeId = retryNode.Id,
                    State = NodeRuntimeState.Waiting,
                    Message = "Transient camera error",
                    ElapsedMs = 25
                };
                retrying.Data[FlowRuntimeDataKeys.Attempt] = 2;
                InvokePrivate(control, "HandleRuntimeEvent", retrying);

                texts = FindChildren<TextBlock>(eventCard).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("重试中", StringComparison.Ordinal) >= 0 && x.IndexOf("第 2 次", StringComparison.Ordinal) >= 0),
                    "NodeRetrying should show the next attempt instead of the generic waiting state.");
                AssertEx.True(texts.Any(x => string.Equals(x, "重试", StringComparison.Ordinal)) &&
                    texts.Any(x => x.IndexOf("3 次", StringComparison.Ordinal) >= 0 && x.IndexOf("1000 ms", StringComparison.Ordinal) >= 0),
                    "Runtime retry status should not hide the card's enabled retry configuration summary.");
                AssertEx.True(Convert.ToString(eventCard.ToolTip, CultureInfo.InvariantCulture).IndexOf("Transient camera error", StringComparison.Ordinal) >= 0,
                    "Retrying cards should keep the retry reason in the tooltip.");

                var recovered = new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.NodeRecovered,
                    NodeId = retryNode.Id,
                    State = NodeRuntimeState.Completed,
                    ElapsedMs = 40
                };
                recovered.Data[FlowRuntimeDataKeys.Attempt] = 2;
                InvokePrivate(control, "HandleRuntimeEvent", recovered);
                InvokePrivate(control, "HandleRuntimeEvent", new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.NodeCompleted,
                    NodeId = retryNode.Id,
                    State = NodeRuntimeState.Completed,
                    ElapsedMs = 42
                });

                texts = FindChildren<TextBlock>(eventCard).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("已恢复", StringComparison.Ordinal) >= 0 && x.IndexOf("第 2 次", StringComparison.Ordinal) >= 0),
                    "NodeCompleted immediately following NodeRecovered should preserve the recovered result on the card.");

                InvokePrivate(control, "HandleRuntimeEvent", new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.NodeCancelled,
                    NodeId = retryNode.Id,
                    State = NodeRuntimeState.Stopped,
                    Message = "Node execution was cancelled.",
                    ElapsedMs = 51
                });
                texts = FindChildren<TextBlock>(eventCard).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("已取消", StringComparison.Ordinal) >= 0),
                    "NodeCancelled should be distinguishable from a generic stopped state.");

                InvokePrivate(control, "HandleRuntimeEvent", new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.NodeSkipped,
                    NodeId = retryNode.Id,
                    State = NodeRuntimeState.Skipped,
                    Message = "All reachable inbound control edges were skipped."
                });
                texts = FindChildren<TextBlock>(eventCard).Select(x => x.Text ?? string.Empty).ToList();
                AssertEx.True(texts.Any(x => x.IndexOf("已跳过", StringComparison.Ordinal) >= 0),
                    "NodeSkipped should show an explicit skipped state on the card.");
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

        private static void InvokePrivate(object instance, string name, params object[] args)
        {
            var method = instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            AssertEx.NotNull(method, "Private method should exist: " + name);
            method.Invoke(instance, args ?? new object[0]);
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
