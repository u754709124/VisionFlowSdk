using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    internal static class GlobalVariableTests
    {
        public static Task SerializationPreservesDefinitionsAndSelectors()
        {
            var runtime = CreateRuntime();
            var json = RuntimeFlowSerializer.Serialize(runtime);
            var restored = RuntimeFlowSerializer.Deserialize(json);

            AssertEx.True(json.Contains("\"GlobalVariables\""),
                "Runtime JSON should contain the global variable protocol field.");
            AssertEx.Equal(3, restored.GlobalVariables.Count,
                "All global variable definitions should round-trip.");
            AssertEx.Equal(string.Empty, restored.GlobalVariables[0].DefaultValue,
                "String global variables should preserve an empty default value.");
            AssertEx.Equal(VariableSelectorScope.GlobalVariable,
                restored.Nodes[0].Settings[FlowSettingNames.DelayMs].Selector.Scope,
                "Global variable selectors should round-trip by stable Id.");
            return Task.FromResult(0);
        }

        public static Task StoreIsTypedAtomicIsolatedAndResetPerRunner()
        {
            var definitions = CreateDefinitions();
            var store = new GlobalVariableStore(definitions);
            AssertEx.Equal(string.Empty, store.Get("lot"),
                "Stores should initialize from defaults, including empty strings.");
            AssertEx.Throws<ArgumentException>(() => store.Set("count", "2"),
                "Global writes must match the declared type exactly.");
            AssertEx.Throws<ArgumentException>(() => store.Set("lot", null),
                "Global values must never be null.");
            store.Set("lot", "LOT-1");
            store.Set("count", 2);
            var snapshot = store.CreateSnapshot(new[] { "lot", "count", "lot" });
            AssertEx.Equal(2, snapshot.Count,
                "A snapshot should atomically capture each requested Id once.");
            AssertEx.Equal("LOT-1", snapshot["lot"],
                "A snapshot should contain the value observed while its single lock is held.");
            AssertEx.Throws<NotSupportedException>(() =>
                ((IDictionary<string, object>)snapshot)["lot"] = "changed",
                "Atomic snapshots should be immutable to callers.");

            var runtime = CreateRuntime();
            var registry = CreateRegistry();
            IFlowRunner first = new FlowEngine(registry).CreateRunner(runtime);
            IFlowRunner second = new FlowEngine(registry).CreateRunner(runtime);
            first.GlobalVariables.Set("lot", "FIRST");
            AssertEx.Equal(string.Empty, second.GlobalVariables.Get("lot"),
                "Different runners must not share Session values.");
            IFlowRunner rebuilt = new FlowEngine(registry).CreateRunner(runtime);
            AssertEx.Equal(string.Empty, rebuilt.GlobalVariables.Get("lot"),
                "Rebuilding a Session must restore configured defaults.");
            return Task.FromResult(0);
        }

        public static Task ValidatorRejectsInvalidDefinitionsAndReferences()
        {
            var runtime = CreateRuntime();
            runtime.GlobalVariables.Add(new GlobalVariableDefinition
            {
                Id = "lot",
                Name = "计数",
                DataType = FlowDataType.Object,
                DefaultValue = null
            });
            runtime.Nodes[0].Settings[FlowSettingNames.DelayMs] =
                NodeSettingValue.ForVariable(VariableSelector.ForGlobalVariable("missing"));
            var result = new FlowValidator(CreateRegistry()).Validate(runtime);
            AssertEx.True(result.Issues.Any(x => x.Code == FlowValidationIssueCodes.GlobalVariableIdInvalid),
                "Duplicate global variable Ids should be rejected.");
            AssertEx.True(result.Issues.Any(x => x.Code == FlowValidationIssueCodes.GlobalVariableNameInvalid),
                "Duplicate global variable Names should be rejected case-insensitively.");
            AssertEx.True(result.Issues.Any(x => x.Code == FlowValidationIssueCodes.GlobalVariableTypeInvalid),
                "Unsupported global variable types should be rejected.");
            AssertEx.True(result.Issues.Any(x => x.Code == FlowValidationIssueCodes.GlobalVariableMissing),
                "Selectors should reject deleted global variable Ids.");
            return Task.FromResult(0);
        }

        public static async Task VariableSetWritesConstantsAndUpstreamOutputs()
        {
            var runtime = new RuntimeFlowDefinition
            {
                FlowId = "global-set",
                FlowName = "Global Set",
                Version = "1.0.0",
                GlobalVariables = new List<GlobalVariableDefinition>
                {
                    StringVariable("source", "来源"),
                    StringVariable("target", "目标")
                }
            };
            runtime.Nodes.Add(CreateGlobalSetNode("set-source", "source", NodeSettingValue.ForConstant("QR-100")));
            runtime.Nodes.Add(CreateGlobalSetNode(
                "set-target",
                "target",
                NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("set-source", FlowOutputNames.Value))));
            runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "set-source",
                FromPort = FlowPortNames.Next,
                ToNodeId = "set-target",
                ToPort = FlowPortNames.In
            });
            runtime.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "set-source" });

            IFlowRunner runner = new FlowEngine(CreateRegistry(), new InMemoryFlowEventSink()).CreateRunner(runtime);
            await runner.StartAsync().ConfigureAwait(false);
            var result = await runner.TriggerAsync(TestTriggerRequests.Manual("ManualStart", null)).ConfigureAwait(false);
            AssertEx.Equal(FlowRunStatus.Succeeded, result.Status,
                "Global variable assignments should complete successfully.");
            AssertEx.Equal("QR-100", runner.GlobalVariables.Get("source"),
                "Constant assignment should update the Session store.");
            AssertEx.Equal("QR-100", runner.GlobalVariables.Get("target"),
                "An upstream output assignment should resolve the current FlowRun output.");
            await runner.StopAsync().ConfigureAwait(false);
        }

        public static Task FlowAwareDescriptorTracksTargetTypeAndDeletion()
        {
            var runtime = CreateRuntime();
            var node = CreateGlobalSetNode("set-count", "count", NodeSettingValue.ForConstant(3));
            runtime.Nodes.Add(node);
            var registry = CreateRegistry();
            var descriptor = registry.ResolveDescriptor(runtime, node);
            AssertEx.Equal(FlowDataType.Int32,
                descriptor.Settings.Single(x => x.Name == FlowSettingNames.Value).DataType,
                "Flow-aware descriptors should constrain Value to the selected global type.");

            runtime.GlobalVariables.RemoveAll(x => x.Id == "count");
            var missingDescriptor = registry.ResolveDescriptor(runtime, node);
            AssertEx.Equal(FlowDataType.Object,
                missingDescriptor.Settings.Single(x => x.Name == FlowSettingNames.Value).DataType,
                "Deleted targets should retain an editable placeholder descriptor.");
            var validation = new FlowValidator(registry).Validate(runtime);
            AssertEx.True(validation.Issues.Any(x =>
                    x.Field == "GlobalVariableId" ||
                    (x.Field != null && x.Field.EndsWith(".GlobalVariableId", StringComparison.OrdinalIgnoreCase))),
                "Deleted global assignment targets should prevent publishing.");
            return Task.FromResult(0);
        }

        public static Task DesignerExposesGlobalsAndOrderedMappingEditor()
        {
            Exception failure = null;
            var thread = new Thread(delegate()
            {
                try
                {
                    var runtime = CreateRuntime();
                    var designer = new FlowDesignerControl(CreateRegistry());
                    designer.LoadDocumentAsync(new FlowDesignDocument
                    {
                        FlowId = runtime.FlowId,
                        FlowName = runtime.FlowName,
                        Runtime = runtime,
                        View = new FlowViewState()
                    }).GetAwaiter().GetResult();
                    designer.UpdateGlobalVariables(runtime.GlobalVariables);
                    var method = typeof(FlowDesignerControl).GetMethod(
                        "CreateVariableSuggestions",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    var suggestions = (IList<VariableSelectionOption>)method.Invoke(
                        designer,
                        new object[] { runtime.Nodes[0] });
                    AssertEx.True(suggestions.Any(x =>
                            x.Selector.Scope == VariableSelectorScope.GlobalVariable &&
                            x.Selector.Path[0] == "count" &&
                            x.DataType == FlowDataType.Int32),
                        "Designer suggestions should expose typed Session globals.");

                    var mappingEditor = new VariableSelectorMappingsControl(
                        suggestions,
                        VariableSelectorScopeFlags.GlobalVariable);
                    mappingEditor.ShowMappings(new object[]
                    {
                        new VariableSelectorFieldMapping
                        {
                            AttributeName = "LotNumber",
                            Source = VariableSelector.ForGlobalVariable("lot")
                        },
                        new VariableSelectorFieldMapping
                        {
                            AttributeName = "Count",
                            Source = VariableSelector.ForGlobalVariable("count")
                        }
                    });
                    var rows = (StackPanel)typeof(VariableSelectorMappingsControl)
                        .GetField("_rows", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(mappingEditor);
                    var firstRow = (Grid)rows.Children[0];
                    var sourceEditor = firstRow.Children.OfType<VariableSelectorControl>().Single();
                    AssertEx.Equal("全局变量.批次", Convert.ToString(sourceEditor.Content),
                        "Mapping selectors should show readable source names instead of stable Ids.");
                    var actionButtons = firstRow.Children.OfType<StackPanel>()
                        .Single()
                        .Children
                        .OfType<Button>()
                        .ToList();
                    AssertEx.Equal(1, actionButtons.Count,
                        "Mapping rows should no longer expose move-up or move-down buttons.");
                    AssertEx.Equal("×", Convert.ToString(actionButtons[0].Content),
                        "Mapping deletion should use the compact multiplication sign.");
                    AssertEx.Equal(Colors.White, ((SolidColorBrush)actionButtons[0].Foreground).Color,
                        "Mapping deletion should render its glyph in white.");
                    AssertEx.Equal(Color.FromRgb(0xD1, 0x43, 0x43),
                        ((SolidColorBrush)actionButtons[0].Background).Color,
                        "Mapping deletion should use the theme error background.");
                    mappingEditor.MoveMapping(1, 0);
                    AssertEx.Equal("Count", mappingEditor.Mappings[0].AttributeName,
                        "Mapping rows should preserve explicit user ordering.");
                    mappingEditor.RemoveMapping(1);
                    AssertEx.Equal(1, mappingEditor.Mappings.Count,
                        "Mapping rows should support deletion.");
                    mappingEditor.AddMapping();
                    AssertEx.True(!string.IsNullOrWhiteSpace(mappingEditor.ValidationError),
                        "New rows should remain invalid until both fields are configured.");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
                throw new InvalidOperationException("Global variable designer verification failed.", failure);
            return Task.FromResult(0);
        }

        public static Task MappingSelectorsUseNestedPublishValidation()
        {
            var runtime = CreateRuntime();
            var target = new NodeDefinition
            {
                Id = "mapping",
                Type = MappingNodeFactory.TypeName,
                Name = "映射",
                Version = "1.0.0"
            };
            target.Settings["FieldMappings"] = NodeSettingValue.ForConstant(new object[]
            {
                new VariableSelectorFieldMapping
                {
                    AttributeName = "Count",
                    Source = VariableSelector.ForNodeOutput("delay", FlowOutputNames.DelayMs)
                },
                new VariableSelectorFieldMapping
                {
                    AttributeName = "Lot",
                    Source = VariableSelector.ForGlobalVariable("lot")
                }
            });
            runtime.Nodes.Add(target);
            runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "delay",
                FromPort = FlowPortNames.Next,
                ToNodeId = "mapping",
                ToPort = FlowPortNames.In
            });
            var registry = CreateRegistry();
            registry.Register(new MappingNodeFactory());
            var valid = new FlowValidator(registry).Validate(runtime);
            AssertEx.False(valid.Issues.Any(x => x.Severity == FlowValidationSeverity.Error),
                "Valid nested NodeOutput and GlobalVariable mappings should publish.");

            target.Settings["FieldMappings"] = NodeSettingValue.ForConstant(new object[]
            {
                new VariableSelectorFieldMapping
                {
                    AttributeName = "Duplicate",
                    Source = VariableSelector.ForNodeOutput("delay", "Missing")
                },
                new VariableSelectorFieldMapping
                {
                    AttributeName = "duplicate",
                    Source = VariableSelector.ForGlobalVariable("missing")
                }
            });
            var invalid = new FlowValidator(registry).Validate(runtime);
            AssertEx.True(invalid.Issues.Any(x => x.Code == FlowValidationIssueCodes.SettingValueInvalid),
                "Mapping AttributeName values should be unique ignoring case.");
            AssertEx.True(invalid.Issues.Any(x => x.Code == FlowValidationIssueCodes.VariableOutputMissing),
                "Nested mappings should validate upstream output names.");
            AssertEx.True(invalid.Issues.Any(x => x.Code == FlowValidationIssueCodes.GlobalVariableMissing),
                "Nested mappings should validate global variable Ids.");
            return Task.FromResult(0);
        }

        private static RuntimeFlowDefinition CreateRuntime()
        {
            var runtime = new RuntimeFlowDefinition
            {
                FlowId = "globals",
                FlowName = "Globals",
                Version = "1.0.0",
                GlobalVariables = CreateDefinitions()
            };
            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "delay",
                Type = DelayNodeFactory.TypeName,
                Name = "延时",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.DelayMs, NodeSettingValue.ForVariable(VariableSelector.ForGlobalVariable("count")) }
                }
            });
            runtime.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "delay" });
            return runtime;
        }

        private static List<GlobalVariableDefinition> CreateDefinitions()
        {
            return new List<GlobalVariableDefinition>
            {
                StringVariable("lot", "批次"),
                new GlobalVariableDefinition { Id = "enabled", Name = "启用", DataType = FlowDataType.Boolean, DefaultValue = false },
                new GlobalVariableDefinition { Id = "count", Name = "计数", DataType = FlowDataType.Int32, DefaultValue = 0 }
            };
        }

        private static GlobalVariableDefinition StringVariable(string id, string name)
        {
            return new GlobalVariableDefinition { Id = id, Name = name, DataType = FlowDataType.String, DefaultValue = string.Empty };
        }

        private static NodeDefinition CreateGlobalSetNode(string id, string targetId, NodeSettingValue value)
        {
            return new NodeDefinition
            {
                Id = id,
                Type = VariableSetNodeFactory.TypeName,
                Name = id,
                Version = "2.0.0",
                Settings =
                {
                    { FlowSettingNames.TargetScope, NodeSettingValue.ForConstant(FlowVariableTargetScope.GlobalVariable.ToString()) },
                    { FlowSettingNames.GlobalVariableId, NodeSettingValue.ForConstant(targetId) },
                    { FlowSettingNames.Value, value }
                }
            };
        }

        private static NodeRegistry CreateRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return registry;
        }

        private sealed class MappingNodeFactory : INodeFactory
        {
            public const string TypeName = "test.mapping";

            public string NodeType { get { return TypeName; } }

            public NodeDescriptor Descriptor
            {
                get
                {
                    return new NodeDescriptor
                    {
                        NodeType = TypeName,
                        DisplayName = "映射",
                        Category = "测试",
                        Version = "1.0.0",
                        InputPorts =
                        {
                            new NodePortDescriptor { Name = FlowPortNames.In, Direction = FlowPortDirection.Input, DataType = FlowDataType.Control, IsRequired = true }
                        },
                        OutputPorts =
                        {
                            new NodePortDescriptor { Name = FlowPortNames.Next, Direction = FlowPortDirection.Output, DataType = FlowDataType.Control }
                        },
                        Settings =
                        {
                            new NodeSettingDescriptor
                            {
                                Name = "FieldMappings",
                                DisplayName = "字段映射",
                                DataType = FlowDataType.Object,
                                IsRequired = true,
                                BindingMode = NodeSettingBindingMode.ConstantOnly,
                                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                                AllowedVariableSources = VariableSelectorScopeFlags.NodeOutput | VariableSelectorScopeFlags.GlobalVariable,
                                EditorKind = NodeSettingEditorKind.VariableSelectorMappings
                            }
                        }
                    };
                }
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return new MappingNode();
            }
        }

        private sealed class MappingNode : IFlowNode
        {
            public Task<NodeExecutionResult> ExecuteAsync(
                FlowExecutionContext context,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(NodeExecutionResult.Success(FlowPortNames.Next));
            }
        }
    }
}
