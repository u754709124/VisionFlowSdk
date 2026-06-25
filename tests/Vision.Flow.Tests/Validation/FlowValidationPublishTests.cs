using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // 校验与发布测试把类似编译器的检查和运行态发布覆盖放在一起。
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

        public static Task InvalidStreamFramesSettingsReturnErrors()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "invalid-stream-frames",
                FlowName = "Invalid Stream Frames",
                Version = "1.0.0"
            };
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Stream Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "CallbackMode", "StreamFrames" },
                    { "MatchMode", "Any" },
                    { "TimeoutMs", 1000 },
                    { "ExpectedFrameCount", 0 },
                    { "FrameTimeoutMs", -1 }
                }
            });
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "callback1" });

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.SettingValueInvalid, "Invalid StreamFrames numeric settings should be reported.");
            AssertEx.True(
                result.Issues.Any(x => string.Equals(x.Field, "Nodes[0].Settings.ExpectedFrameCount", StringComparison.OrdinalIgnoreCase)),
                "ExpectedFrameCount field should be reported.");
            AssertEx.True(
                result.Issues.Any(x => string.Equals(x.Field, "Nodes[0].Settings.FrameTimeoutMs", StringComparison.OrdinalIgnoreCase)),
                "FrameTimeoutMs field should be reported.");
            return Task.FromResult(0);
        }

        public static Task InvalidQueueAndGroupSettingsReturnErrors()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "invalid-node-settings",
                FlowName = "Invalid Node Settings",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "save1",
                Type = ImageSaveNodeFactory.TypeName,
                Name = "Queued Save",
                Version = "1.0.0",
                Settings =
                {
                    { "UseQueue", true },
                    { "QueueCapacity", 0 },
                    { "QueueMaxDegreeOfParallelism", 0 },
                    { "QueueFullMode", "Bogus" }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "scanJoin1",
                Type = ScanGroupJoinNodeFactory.TypeName,
                Name = "Scan Join",
                Version = "1.0.0",
                Settings =
                {
                    { "ExpectedFrameCount", 0 },
                    { "DuplicatePolicy", "KeepFirst" },
                    { "RequireContinuousFrameIndex", true },
                    { "FirstFrameIndex", -1 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "save1" });

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, FlowValidationIssueCodes.QueueFullModeInvalid, "Invalid QueueFullMode should be reported.");
            AssertHasIssue(result, FlowValidationIssueCodes.DuplicatePolicyInvalid, "Invalid DuplicatePolicy should be reported.");
            AssertEx.True(
                result.Issues.Count(x => string.Equals(x.Code, FlowValidationIssueCodes.SettingValueInvalid, StringComparison.OrdinalIgnoreCase)) >= 4,
                "Invalid queue/group numeric settings should be reported.");
            return Task.FromResult(0);
        }

        public static Task PublishRuntimeDoesNotContainViewState()
        {
            var document = CreateValidDesignDocument();
            document.View.Zoom = 1.5;
            document.View.OffsetX = 24;
            document.View.OffsetY = 42;
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
