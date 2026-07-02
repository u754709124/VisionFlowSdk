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
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
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
    // У���뷢�����԰����Ʊ������ļ�������̬�������Ƿ���һ��
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
                    { "DelayMs", 0 }
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

        public static Task MissingBindingOutputReturnsError()
        {
            var flow = CreateValidRuntime();
            flow.Nodes[1].InputBindings["Message"] = VariableBinding.ForVariable("delay1", "MissingOutput");

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.BindingOutputMissing, "Missing source output should be reported.");
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
                    { "DelayMs", -1 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = AndJoinNodeFactory.TypeName,
                Name = "Join",
                Version = "1.0.0",
                Settings =
                {
                    { "JoinKeyBinding", "{{ token.PositionId }}" },
                    { "ExpectedInputCount", 0 },
                    { "TimeoutMs", -1 },
                    { "DuplicatePolicy", "KeepFirst" }
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
                    { "LeftBinding", "{{ token.PositionId }}" },
                    { "Operator", "Bogus" }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "delay1" });

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.SettingValueInvalid, "Invalid core numeric/operator settings should be reported.");
            AssertHasIssue(result, FlowValidationIssueCodes.DuplicatePolicyInvalid, "Invalid DuplicatePolicy should be reported.");
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
            var result = new FlowPublishService(CreateRegistry()).Publish(CreateValidDesignDocument());

            AssertEx.True(result.IsSuccess, "Valid design should publish successfully.");
            AssertEx.NotNull(result.Runtime, "Publish result should include runtime definition.");
            AssertEx.Equal("validation-publish", result.Runtime.FlowId, "Published runtime FlowId should be preserved.");
            AssertEx.Equal(2, result.Runtime.Nodes.Count, "Published runtime nodes should be preserved.");
            AssertEx.Equal(1, result.Runtime.Edges.Count, "Published runtime edges should be preserved.");
            AssertEx.Equal(1, result.Runtime.Entries.Count, "Published runtime entries should be preserved.");
            AssertEx.Equal("delay1.DelayMs", result.Runtime.Nodes[1].InputBindings["Message"].GetVariableName(), "Input binding should be preserved.");
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
                    { "DelayMs", 0 }
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
                    { "Level", "Info" }
                },
                InputBindings =
                {
                    { "Message", VariableBinding.ForVariable("delay1", "DelayMs") }
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
    }
}
