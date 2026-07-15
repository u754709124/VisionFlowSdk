using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Nodes;
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
    // 鏍￠獙涓庡彂甯冩祴璇曟妸绫讳技缂栬瘧鍣ㄧ殑妫€鏌ュ拰杩愯鎬佸彂甯冭鐩栨斁鍦ㄤ竴璧枫€?
    internal static class FlowValidationPublishTests
    {
        public static Task DuplicateNodeIdReturnsError()
        {
            var flow = CreateValidRuntime();
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "delay1",
                Type = DelayNodeFactory.TypeName,
                Name = "Duplicate Delay",
                Version = "1.0.0",
                Settings =
                {
                    { "DelayMs", NodeSettingValue.ForConstant(0) }
                }
            });
            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.NodeIdDuplicate, "Duplicate NodeId should be reported.");
            return Task.FromResult(0);
        }

        public static Task DanglingEdgeReturnsError()
        {
            var flow = CreateValidRuntime();
            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "delay1",
                FromPort = "Next",
                ToNodeId = "missing-node",
                ToPort = "In"
            });

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.EdgeTargetMissing, "Dangling edge target should be reported.");
            return Task.FromResult(0);
        }

        public static Task MissingRequiredSettingReturnsError()
        {
            var flow = CreateValidRuntime();
            flow.Nodes[0].Settings.Clear();

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.RequiredSettingMissing, "Missing required DelayMs should be reported.");
            return Task.FromResult(0);
        }

        public static Task MissingVariableOutputReturnsError()
        {
            var flow = CreateValidRuntime();
            flow.Nodes[1].Settings["Message"] = NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("delay1", "MissingOutput"));

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.VariableOutputMissing, "Missing source output should be reported.");
            return Task.FromResult(0);
        }

        public static Task InvalidCoreNodeSettingsReturnErrors()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "invalid-core-node-settings",
                FlowName = "Invalid Core Node Settings",
                Version = "1.0.0"
            };
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "delay1",
                Type = DelayNodeFactory.TypeName,
                Name = "Delay",
                Version = "1.0.0",
                Settings =
                {
                    { "DelayMs", NodeSettingValue.ForConstant(-1) }
                }
            });
            flow.Nodes[0].ExecutionPolicy.TimeoutMs = -1;
            flow.Nodes[0].ExecutionPolicy.MaxConcurrentExecutions = 0;
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.MaxRetries = -1;
            flow.Nodes[0].ExecutionPolicy.RetryPolicy.RetryIntervalMs = -1;
            flow.Nodes[0].ExecutionPolicy.FailureStrategy = FailureStrategy.ErrorBranch;

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = AndJoinNodeFactory.TypeName,
                Name = "Join",
                Version = "1.0.0",
                Settings =
                {
                    { "JoinKeyBinding", NodeSettingValue.ForVariable(VariableSelector.ForToken("PositionId")) },
                    { "ExpectedInputCount", NodeSettingValue.ForConstant(0) },
                    { "TimeoutMs", NodeSettingValue.ForConstant(-1) },
                    { "DuplicatePolicy", NodeSettingValue.ForConstant("KeepFirst") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "condition1",
                Type = ConditionNodeFactory.TypeName,
                Name = "Condition",
                Version = "1.0.0",
                Settings =
                {
                    { "LeftBinding", NodeSettingValue.ForVariable(VariableSelector.ForToken("PositionId")) },
                    { "Operator", NodeSettingValue.ForConstant("Bogus") }
                }
            });
            flow.Nodes[2].ExecutionPolicy.FailureStrategy = FailureStrategy.DefaultOutputs;
            flow.Nodes[2].ExecutionPolicy.DefaultOutputs["Undeclared"] = "bad";

            var noErrorPortNode = new NodeDefinition
            {
                Id = "noErrorPort1",
                Type = NoErrorPortFactory.TypeName,
                Name = "No Error Port",
                Version = "1.0.0"
            };
            noErrorPortNode.ExecutionPolicy.FailureStrategy = FailureStrategy.ErrorBranch;
            flow.Nodes.Add(noErrorPortNode);

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "delay1" });

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.SettingValueInvalid, "Invalid core numeric/operator settings should be reported.");
            AssertHasIssue(result, FlowValidationIssueCodes.DuplicatePolicyInvalid, "Invalid DuplicatePolicy should be reported.");
            AssertHasIssue(result, FlowValidationIssueCodes.NodeExecutionPolicyInvalid, "Invalid node policy numbers should be reported.");
            AssertHasIssue(result, FlowValidationIssueCodes.NodeErrorPortMissing, "ErrorBranch should require an Error control port.");
            AssertHasIssue(result, FlowValidationIssueCodes.NodeDefaultOutputInvalid, "DefaultOutputs should match descriptor outputs.");
            AssertEx.True(
                result.Issues.Count(x => string.Equals(x.Code, FlowValidationIssueCodes.SettingValueInvalid, StringComparison.OrdinalIgnoreCase)) >= 4,
                "Invalid core node numeric/operator settings should be reported.");
            return Task.FromResult(0);
        }

        public static Task PublishRuntimeDoesNotContainViewState()
        {
            var document = CreateValidDesignDocument();
            document.View.Zoom = 1.5;
            document.View.OffsetX = 24;
            document.View.OffsetY = 42;
            document.View.CanvasWidth = 2400;
            document.View.CanvasHeight = 1600;
            document.View.Nodes["delay1"] = new NodeViewState
            {
                X = 100,
                Y = 200,
                IsCollapsed = true
            };

            var result = new FlowPublishService(CreateRegistry()).Publish(document);
            var json = RuntimeFlowSerializer.Serialize(result.Runtime);

            AssertEx.True(result.IsSuccess, "Valid design should publish successfully.");
            AssertEx.False(object.ReferenceEquals(document.Runtime, result.Runtime), "Publish should create a runtime copy.");
            AssertEx.False(json.IndexOf("View", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime must not contain view state.");
            AssertEx.False(json.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime must not contain canvas zoom.");
            AssertEx.False(json.IndexOf("OffsetX", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime must not contain canvas offsets.");
            AssertEx.False(json.IndexOf("CanvasWidth", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime must not contain canvas width.");
            AssertEx.False(json.IndexOf("CanvasHeight", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime must not contain canvas height.");
            AssertEx.False(json.IndexOf("NodeViewState", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime must not contain designer view types.");
            AssertEx.False(json.IndexOf("IsCollapsed", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime must not contain node collapsed state.");
            return Task.FromResult(0);
        }

        public static Task ValidFlowPublishesSuccessfully()
        {
            var document = CreateValidDesignDocument();
            document.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy.Enabled = true;
            document.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy.MaxRetries = 2;
            document.Runtime.Nodes[0].ExecutionPolicy.DefaultOutputs["Mutable"] = new List<object> { "source" };
            var result = new FlowPublishService(CreateRegistry()).Publish(document);

            AssertEx.True(result.IsSuccess, "Valid design should publish successfully.");
            AssertEx.NotNull(result.Runtime, "Publish result should include runtime definition.");
            AssertEx.Equal("validation-publish", result.Runtime.FlowId, "Published runtime FlowId should be preserved.");
            AssertEx.Equal(2, result.Runtime.Nodes.Count, "Published runtime nodes should be preserved.");
            AssertEx.Equal(1, result.Runtime.Edges.Count, "Published runtime edges should be preserved.");
            AssertEx.Equal(1, result.Runtime.Entries.Count, "Published runtime entries should be preserved.");
            AssertEx.Equal(NodeSettingValueMode.Variable, result.Runtime.Nodes[1].Settings["Message"].Mode, "Variable setting should be preserved.");
            AssertEx.Equal("delay1", result.Runtime.Nodes[1].Settings["Message"].Selector.Path[0], "Variable source should be preserved.");
            AssertEx.False(object.ReferenceEquals(document.Runtime.Nodes[0].ExecutionPolicy, result.Runtime.Nodes[0].ExecutionPolicy),
                "Publish should clone node execution policies.");
            AssertEx.False(object.ReferenceEquals(document.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy, result.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy),
                "Publish should clone nested retry policies.");
            AssertEx.False(object.ReferenceEquals(document.Runtime.Nodes[0].ExecutionPolicy.DefaultOutputs, result.Runtime.Nodes[0].ExecutionPolicy.DefaultOutputs),
                "Publish should clone default output dictionaries.");
            document.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy.MaxRetries = 99;
            ((List<object>)document.Runtime.Nodes[0].ExecutionPolicy.DefaultOutputs["Mutable"])[0] = "changed";
            AssertEx.Equal(2, result.Runtime.Nodes[0].ExecutionPolicy.RetryPolicy.MaxRetries,
                "Published retry policy should not alias the design document.");
            AssertEx.Equal("source", Convert.ToString(((List<object>)result.Runtime.Nodes[0].ExecutionPolicy.DefaultOutputs["Mutable"])[0]),
                "Published default output values should be deep-cloned.");
            return Task.FromResult(0);
        }

        public static Task PublishRejectsV1Schemas()
        {
            var document = CreateValidDesignDocument();
            document.SchemaVersion = 1;
            AssertEx.Throws<UnsupportedFlowSchemaVersionException>(
                () => new FlowPublishService(CreateRegistry()).Publish(document),
                "Publishing a v1 design schema should fail.");

            document = CreateValidDesignDocument();
            document.Runtime.SchemaVersion = 1;
            AssertEx.Throws<UnsupportedFlowSchemaVersionException>(
                () => new FlowPublishService(CreateRegistry()).Publish(document),
                "Publishing a v1 runtime schema should fail.");
            return Task.FromResult(0);
        }

        private static FlowValidator CreateValidator()
        {
            return new FlowValidator(CreateRegistry());
        }

        private static NodeRegistry CreateRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new NoErrorPortFactory());
            return registry;
        }

        private static FlowDesignDocument CreateValidDesignDocument()
        {
            return new FlowDesignDocument
            {
                FlowId = "validation-publish",
                FlowName = "Validation Publish",
                Runtime = CreateValidRuntime(),
                View = new FlowViewState()
            };
        }

        private static RuntimeFlowDefinition CreateValidRuntime()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "validation-publish",
                FlowName = "Validation Publish",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "delay1",
                Type = DelayNodeFactory.TypeName,
                Name = "Delay",
                Version = "1.0.0",
                Settings =
                {
                    { "DelayMs", NodeSettingValue.ForConstant(0) }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "log1",
                Type = LogNodeFactory.TypeName,
                Name = "Log",
                Version = "1.0.0",
                Settings =
                {
                    { "Level", NodeSettingValue.ForConstant("Info") },
                    { "Message", NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("delay1", "DelayMs")) }
                }
            });

            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "delay1",
                FromPort = "Next",
                ToNodeId = "log1",
                ToPort = "In"
            });
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "delay1" });
            return flow;
        }

        private static void AssertHasIssue(FlowValidationResult result, string code, string message)
        {
            AssertEx.True(
                result.Issues.Any(x => x.Severity == FlowValidationSeverity.Error && string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)),
                message + " Issues: " + string.Join(", ", result.Issues.Select(x => x.Code)));
        }

        private sealed class NoErrorPortFactory : INodeFactory
        {
            public const string TypeName = "test.no-error-port";

            public string NodeType
            {
                get { return TypeName; }
            }

            public NodeDescriptor Descriptor
            {
                get
                {
                    return new NodeDescriptor
                    {
                        NodeType = TypeName,
                        DisplayName = "无错误端口测试节点",
                        Category = "测试",
                        Version = "1.0.0",
                        OutputPorts =
                        {
                            new NodePortDescriptor
                            {
                                Name = FlowPortNames.Next,
                                DisplayName = FlowPortNames.Next,
                                Direction = FlowPortDirection.Output,
                                DataType = FlowDataType.Control
                            }
                        }
                    };
                }
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return null;
            }
        }
    }
}
