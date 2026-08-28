using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    internal static class DynamicDescriptorTests
    {
        public static Task RegistryResolvesStaticAndInstanceDescriptors()
        {
            var registry = CreateRegistry();
            var paletteDescriptor = registry.Descriptors.Single(
                x => string.Equals(x.NodeType, DynamicDescriptorFactory.TypeName, StringComparison.OrdinalIgnoreCase));
            var shapingSetting = paletteDescriptor.Settings.Single(x => x.Name == DynamicDescriptorFactory.CommandSetting);

            AssertEx.True(shapingSetting.AffectsDescriptor, "The shaping setting should declare that it affects the descriptor.");
            AssertEx.Equal(1, paletteDescriptor.Settings.Count, "The palette descriptor should only expose stable shaping settings.");

            var alphaNode = CreateDynamicNode("Alpha", includeCommandInput: true);
            var alphaDescriptor = registry.ResolveDescriptor(alphaNode);
            AssertEx.True(alphaDescriptor.Settings.Any(x => x.Name == "AlphaInput"), "Alpha instance should expose AlphaInput.");
            AssertEx.True(alphaDescriptor.Outputs.Any(x => x.Name == "AlphaResult"), "Alpha instance should expose AlphaResult.");

            var betaNode = CreateDynamicNode("Beta", includeCommandInput: true);
            var betaDescriptor = registry.ResolveDescriptor(betaNode);
            AssertEx.True(betaDescriptor.Settings.Any(x => x.Name == "BetaInput"), "Beta instance should expose BetaInput.");
            AssertEx.True(betaDescriptor.Outputs.Any(x => x.Name == "BetaResult"), "Beta instance should expose BetaResult.");
            AssertEx.False(betaDescriptor.Outputs.Any(x => x.Name == "AlphaResult"), "Beta instance should not retain Alpha outputs.");

            var staticFactory = new StaticDescriptorFactory();
            registry.Register(staticFactory);
            var staticNode = new NodeDefinition { Id = "static1", Type = StaticDescriptorFactory.TypeName };
            AssertEx.True(
                object.ReferenceEquals(staticFactory.Descriptor, registry.ResolveDescriptor(staticNode)),
                "Factories without the optional provider should keep using their static descriptor.");
            return Task.FromResult(0);
        }

        public static Task ValidatorUsesInstanceDescriptorContracts()
        {
            var registry = CreateRegistry();
            var flow = CreateDynamicFlow("Alpha", includeCommandInput: false);
            var validator = new FlowValidator(registry);

            var missingInput = validator.Validate(flow);
            AssertHasIssue(
                missingInput,
                FlowValidationIssueCodes.RequiredSettingMissing,
                "The required setting from the Alpha instance descriptor should be validated.");

            flow.Nodes[0].Settings["AlphaInput"] = NodeSettingValue.ForConstant("alpha");
            var validAlpha = validator.Validate(flow);
            AssertEx.True(
                validAlpha.IsValid,
                "A flow satisfying the Alpha instance descriptor should be valid. Issues: " +
                string.Join(", ", validAlpha.Issues.Select(x => x.Code)));

            flow.Nodes[0].Settings[DynamicDescriptorFactory.CommandSetting] = NodeSettingValue.ForConstant("Beta");
            flow.Nodes[0].Settings["BetaInput"] = NodeSettingValue.ForConstant("beta");
            var staleOutput = validator.Validate(flow);
            AssertHasIssue(
                staleOutput,
                FlowValidationIssueCodes.VariableOutputMissing,
                "Changing the source instance descriptor should invalidate a selector for the removed output.");

            flow.Nodes[1].Settings["Message"] =
                NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("dynamic1", "BetaResult"));
            flow.Nodes[0].ExecutionPolicy.FailureStrategy = FailureStrategy.DefaultOutputs;
            flow.Nodes[0].ExecutionPolicy.DefaultOutputs["AlphaResult"] = "stale";
            var staleDefaultOutput = validator.Validate(flow);
            AssertHasIssue(
                staleDefaultOutput,
                FlowValidationIssueCodes.NodeDefaultOutputInvalid,
                "DefaultOutputs should be checked against the resolved instance outputs.");
            return Task.FromResult(0);
        }

        public static Task ValidatorReportsDescriptorResolutionFailures()
        {
            var flow = CreateDynamicFlow("Throw", includeCommandInput: false);
            var result = new FlowValidator(CreateRegistry()).Validate(flow);
            var issue = result.Errors.Single(
                x => string.Equals(
                    x.Code,
                    FlowValidationIssueCodes.NodeDescriptorResolutionFailed,
                    StringComparison.OrdinalIgnoreCase));

            AssertEx.Equal("dynamic1", issue.NodeId, "Descriptor resolution failure should identify the node instance.");
            AssertEx.True(
                issue.Message.IndexOf("descriptor test failure", StringComparison.OrdinalIgnoreCase) >= 0,
                "Descriptor resolution failure should preserve the provider diagnostic.");
            return Task.FromResult(0);
        }

        public static Task DynamicDescriptorSettingsKeepSchemaV3Serialization()
        {
            var runtime = CreateDynamicFlow("Beta", includeCommandInput: true);
            runtime.Nodes[1].Settings["Message"] =
                NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("dynamic1", "BetaResult"));
            var runtimeJson = RuntimeFlowSerializer.Serialize(runtime);
            var restoredRuntime = RuntimeFlowSerializer.Deserialize(runtimeJson);

            AssertEx.Equal(FlowSchema.CurrentVersion, restoredRuntime.SchemaVersion, "Runtime schema should remain v3.");
            AssertEx.False(
                runtimeJson.IndexOf("AffectsDescriptor", StringComparison.OrdinalIgnoreCase) >= 0,
                "Runtime files must not persist descriptor metadata.");
            AssertEx.False(
                runtimeJson.IndexOf("AlphaResult", StringComparison.OrdinalIgnoreCase) >= 0,
                "Runtime files should contain only the selected instance settings and bindings.");
            AssertEx.Equal(
                "Beta",
                Convert.ToString(
                    restoredRuntime.Nodes[0].Settings[DynamicDescriptorFactory.CommandSetting].ConstantValue,
                    CultureInfo.InvariantCulture),
                "The descriptor-shaping setting should round-trip as a normal node setting.");
            AssertEx.True(
                CreateRegistry().ResolveDescriptor(restoredRuntime.Nodes[0]).Outputs.Any(x => x.Name == "BetaResult"),
                "A deserialized node should resolve the same instance descriptor.");

            var design = new FlowDesignDocument
            {
                FlowId = runtime.FlowId,
                FlowName = runtime.FlowName,
                Runtime = runtime,
                View = new FlowViewState()
            };
            var designJson = FlowDesignSerializer.Serialize(design);
            var restoredDesign = FlowDesignSerializer.Deserialize(designJson);
            AssertEx.Equal(FlowSchema.CurrentVersion, restoredDesign.SchemaVersion, "Design schema should remain v3.");
            AssertEx.False(
                designJson.IndexOf("AffectsDescriptor", StringComparison.OrdinalIgnoreCase) >= 0,
                "Design files must not persist descriptor metadata.");
            return Task.FromResult(0);
        }

        public static Task TypedObjectSelectorsValidateRootsAndFirstLayerMembers()
        {
            var registry = new NodeRegistry();
            registry.Register(new TypedObjectSourceFactory());
            registry.Register(new TypedObjectTargetFactory());
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "typed-object-selectors",
                FlowName = "Typed Object Selectors",
                Version = "1.0.0"
            };
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "source",
                Type = TypedObjectSourceFactory.TypeName,
                Name = "Source",
                Version = "1.0.0"
            });
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "target",
                Type = TypedObjectTargetFactory.TypeName,
                Name = "Target",
                Version = "1.0.0",
                Settings =
                {
                    {
                        "Payload",
                        NodeSettingValue.ForVariable(
                            VariableSelector.ForNodeOutput("source", "Payload"))
                    },
                    {
                        "Name",
                        NodeSettingValue.ForVariable(new VariableSelector
                        {
                            Scope = VariableSelectorScope.NodeOutput,
                            Path = new System.Collections.Generic.List<string>
                            {
                                "source",
                                "Payload",
                                "Name"
                            }
                        })
                    }
                }
            });
            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "source",
                FromPort = FlowPortNames.Next,
                ToNodeId = "target",
                ToPort = FlowPortNames.In
            });
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "source"
            });

            var validator = new FlowValidator(registry);
            FlowValidationResult valid = validator.Validate(flow);
            AssertEx.True(valid.IsValid,
                "Typed root and first-level property selectors should validate. Issues: " +
                string.Join(", ", valid.Issues.Select(x => x.Code)));

            flow.Nodes[1].Settings["Name"] =
                NodeSettingValue.ForVariable(new VariableSelector
                {
                    Scope = VariableSelectorScope.NodeOutput,
                    Path = new System.Collections.Generic.List<string>
                    {
                        "source",
                        "Payload",
                        "Nested",
                        "Code"
                    }
                });
            FlowValidationResult nested = validator.Validate(flow);
            AssertHasIssue(
                nested,
                FlowValidationIssueCodes.VariableSelectorInvalid,
                "Typed Object selectors must stop after one member segment.");
            return Task.FromResult(0);
        }

        private static NodeRegistry CreateRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new DynamicDescriptorFactory());
            return registry;
        }

        private static RuntimeFlowDefinition CreateDynamicFlow(string command, bool includeCommandInput)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "dynamic-descriptor",
                FlowName = "Dynamic Descriptor",
                Version = "1.0.0"
            };
            flow.Nodes.Add(CreateDynamicNode(command, includeCommandInput));
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "log1",
                Type = LogNodeFactory.TypeName,
                Name = "Log",
                Version = "1.0.0",
                Settings =
                {
                    { "Level", NodeSettingValue.ForConstant("Info") },
                    { "Message", NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("dynamic1", "AlphaResult")) }
                }
            });
            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "dynamic1",
                FromPort = FlowPortNames.Next,
                ToNodeId = "log1",
                ToPort = FlowPortNames.In
            });
            flow.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "dynamic1"
            });
            return flow;
        }

        private static NodeDefinition CreateDynamicNode(string command, bool includeCommandInput)
        {
            var node = new NodeDefinition
            {
                Id = "dynamic1",
                Type = DynamicDescriptorFactory.TypeName,
                Name = "Dynamic",
                Version = "1.0.0"
            };
            node.Settings[DynamicDescriptorFactory.CommandSetting] = NodeSettingValue.ForConstant(command);
            if (includeCommandInput && !string.Equals(command, "Throw", StringComparison.OrdinalIgnoreCase))
            {
                node.Settings[command + "Input"] = NodeSettingValue.ForConstant(command.ToLowerInvariant());
            }

            return node;
        }

        private static void AssertHasIssue(FlowValidationResult result, string code, string message)
        {
            AssertEx.True(
                result.Errors.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)),
                message + " Issues: " + string.Join(", ", result.Issues.Select(x => x.Code)));
        }

        private sealed class DynamicDescriptorFactory : INodeFactory, IInstanceNodeDescriptorProvider
        {
            public const string TypeName = "test.dynamic-descriptor";
            public const string CommandSetting = "Command";

            private readonly NodeDescriptor _descriptor = CreateBaseDescriptor();

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
                NodeSettingValue commandValue;
                var command = definition.Settings != null &&
                    definition.Settings.TryGetValue(CommandSetting, out commandValue) &&
                    commandValue != null
                    ? Convert.ToString(commandValue.ConstantValue, CultureInfo.InvariantCulture)
                    : "Alpha";
                if (string.Equals(command, "Throw", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("descriptor test failure");
                }

                var normalizedCommand = string.Equals(command, "Beta", StringComparison.OrdinalIgnoreCase)
                    ? "Beta"
                    : "Alpha";
                var descriptor = CreateBaseDescriptor();
                descriptor.Settings.Add(new NodeSettingDescriptor
                {
                    Name = normalizedCommand + "Input",
                    DisplayName = normalizedCommand + " Input",
                    DataType = FlowDataType.String,
                    IsRequired = true,
                    BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                    EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                    AllowedVariableSources = VariableSelectorScopeFlags.All
                });
                descriptor.Outputs.Add(new NodeOutputDescriptor
                {
                    Name = normalizedCommand + "Result",
                    DisplayName = normalizedCommand + " Result",
                    DataType = FlowDataType.String
                });
                return descriptor;
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return null;
            }

            private static NodeDescriptor CreateBaseDescriptor()
            {
                return new NodeDescriptor
                {
                    NodeType = TypeName,
                    DisplayName = "动态描述符测试节点",
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
                        }
                    }
                };
            }
        }

        private sealed class StaticDescriptorFactory : INodeFactory
        {
            public const string TypeName = "test.static-descriptor";
            private readonly NodeDescriptor _descriptor = new NodeDescriptor { NodeType = TypeName };

            public string NodeType
            {
                get { return TypeName; }
            }

            public NodeDescriptor Descriptor
            {
                get { return _descriptor; }
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return null;
            }
        }

        private sealed class TypedObjectPayload
        {
            public string Name { get; set; }

            public TypedObjectNestedPayload Nested { get; set; }
        }

        private sealed class TypedObjectNestedPayload
        {
            public string Code { get; set; }
        }

        private sealed class TypedObjectSourceFactory : INodeFactory
        {
            public const string TypeName = "test.typed-object-source";

            public string NodeType { get { return TypeName; } }

            public NodeDescriptor Descriptor
            {
                get
                {
                    return new NodeDescriptor
                    {
                        NodeType = TypeName,
                        DisplayName = "类型化对象来源",
                        Category = "测试",
                        Version = "1.0.0",
                        OutputPorts =
                        {
                            new NodePortDescriptor { Name = FlowPortNames.Next, Direction = FlowPortDirection.Output, DataType = FlowDataType.Control }
                        },
                        Outputs =
                        {
                            new NodeOutputDescriptor { Name = "Payload", DataType = FlowDataType.Object, ObjectType = typeof(TypedObjectPayload) }
                        }
                    };
                }
            }

            public IFlowNode Create(NodeDefinition definition) { return null; }
        }

        private sealed class TypedObjectTargetFactory : INodeFactory
        {
            public const string TypeName = "test.typed-object-target";

            public string NodeType { get { return TypeName; } }

            public NodeDescriptor Descriptor
            {
                get
                {
                    return new NodeDescriptor
                    {
                        NodeType = TypeName,
                        DisplayName = "类型化对象目标",
                        Category = "测试",
                        Version = "1.0.0",
                        InputPorts =
                        {
                            new NodePortDescriptor { Name = FlowPortNames.In, Direction = FlowPortDirection.Input, DataType = FlowDataType.Control, IsRequired = true }
                        },
                        Settings =
                        {
                            new NodeSettingDescriptor
                            {
                                Name = "Payload",
                                DataType = FlowDataType.Object,
                                ObjectType = typeof(TypedObjectPayload),
                                IsRequired = true,
                                BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                                AllowedVariableSources = VariableSelectorScopeFlags.NodeOutput
                            },
                            new NodeSettingDescriptor
                            {
                                Name = "Name",
                                DataType = FlowDataType.String,
                                IsRequired = true,
                                BindingMode = NodeSettingBindingMode.ConstantOrVariable,
                                EvaluationPhase = NodeSettingEvaluationPhase.Execution,
                                AllowedVariableSources = VariableSelectorScopeFlags.NodeOutput
                            }
                        }
                    };
                }
            }

            public IFlowNode Create(NodeDefinition definition) { return null; }
        }
    }
}
