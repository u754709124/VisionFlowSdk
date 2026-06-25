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
    // 运控节点测试通过 Fake 设备覆盖适配器转接的运动行为。
    internal static class MotionNodeTests
    {
        public static async Task MotionNotifyWithFakeMotion()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var motion = new FakeMotionAdapter("Motion01");
            var runner = CreateRunner(CreateNotifyFlow(includeErrorHandler: false), executionLog, sink, motion);
            var token = new FlowToken
            {
                TokenId = "motion-token",
                PositionId = "P01",
                CaptureGroupId = "CAP-01",
                ScanGroupId = "SCAN-01"
            };
            token.Set("InspectionResult", "OK");

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);

            var message = motion.SnapshotLastMessage();
            AssertEx.NotNull(message, "motion.notify should send a message to FakeMotionAdapter.");
            AssertEx.Equal("PhotoDone", message.MessageType, "Motion message type should match settings.");
            AssertEx.Equal("Motion01", message.MotionId, "Motion message should target the configured motion id.");
            AssertEx.Equal("P01", message.PositionId, "Motion message should resolve PositionId from token binding.");
            AssertEx.Equal("CAP-01", message.CaptureGroupId, "Motion message should resolve CaptureGroupId from token binding.");
            AssertEx.Equal("SCAN-01", message.ScanGroupId, "Motion message should resolve ScanGroupId from token binding.");
            AssertEx.Equal("OK", message.Result, "Motion message should resolve Result from token values.");
        }

        public static async Task MotionMoveToWithFakeMotion()
        {
            var motion = new FakeMotionAdapter("Motion01");
            var runner = CreateRunner(CreatePositionFlow(MotionMoveToNodeFactory.TypeName, "Load"), new List<string>(), new InMemoryFlowEventSink(), motion);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "motion-move" }).ConfigureAwait(false);

            AssertEx.Equal("Load", motion.CurrentPosition, "motion.move_to should move FakeMotionAdapter to the configured position.");
        }

        public static async Task MotionWaitInPositionWithFakeMotion()
        {
            var motion = new FakeMotionAdapter("Motion01");
            var runner = CreateRunner(CreatePositionFlow(MotionWaitInPositionNodeFactory.TypeName, "Ready"), new List<string>(), new InMemoryFlowEventSink(), motion);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "motion-wait" }).ConfigureAwait(false);

            AssertEx.Equal("Ready", motion.CurrentPosition, "motion.wait_in_position should wait or move FakeMotionAdapter to the configured position.");
        }

        public static async Task MissingMotionIdRoutesError()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var motion = new FakeMotionAdapter("Motion01");
            var runner = CreateRunner(CreateMissingMotionIdFlow(), executionLog, sink, motion);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "motion-missing" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "ErrorHandler" }, executionLog, "Missing MotionId should route through Error.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeFailed && string.Equals(x.NodeId, "motion1", StringComparison.OrdinalIgnoreCase)),
                "Missing MotionId should publish NodeFailed.");
        }

        private static IFlowRunner CreateRunner(RuntimeFlowDefinition flow, IList<string> executionLog, InMemoryFlowEventSink sink, FakeMotionAdapter motion)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new RecordingNodeFactory(executionLog));

            var devices = new DefaultDeviceRegistry()
                .RegisterMotion(motion);

            return new FlowEngine(registry, sink, devices).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateNotifyFlow(bool includeErrorHandler)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "motion-notify",
                FlowName = "Motion Notify",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "motion1",
                Type = MotionNotifyNodeFactory.TypeName,
                Name = "Notify",
                Version = "1.0.0",
                Settings =
                {
                    { "MotionId", "Motion01" },
                    { "MessageType", "PhotoDone" },
                    { "PositionIdBinding", "{{ token.PositionId }}" },
                    { "CaptureGroupIdBinding", "{{ token.CaptureGroupId }}" },
                    { "ScanGroupIdBinding", "{{ token.ScanGroupId }}" },
                    { "ResultBinding", "{{ token.InspectionResult }}" },
                    { "TimeoutMs", 1000 }
                }
            });

            if (includeErrorHandler)
            {
                flow.Nodes.Add(CreateRecordNode("ErrorHandler"));
                flow.Edges.Add(CreateEdge("motion1", "Error", "ErrorHandler"));
            }

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "motion1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreatePositionFlow(string nodeType, string positionName)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = nodeType.Replace('.', '-') + "-flow",
                FlowName = nodeType,
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "motion1",
                Type = nodeType,
                Name = nodeType,
                Version = "1.0.0",
                Settings =
                {
                    { "MotionId", "Motion01" },
                    { "PositionName", positionName },
                    { "TimeoutMs", 1000 }
                }
            });
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "motion1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateMissingMotionIdFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "motion-missing-id",
                FlowName = "Motion Missing Id",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "motion1",
                Type = MotionNotifyNodeFactory.TypeName,
                Name = "Notify",
                Version = "1.0.0",
                Settings =
                {
                    { "MessageType", "PhotoDone" },
                    { "TimeoutMs", 1000 }
                }
            });
            flow.Nodes.Add(CreateRecordNode("ErrorHandler"));
            flow.Edges.Add(CreateEdge("motion1", "Error", "ErrorHandler"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "motion1" });
            return flow;
        }

        private static NodeDefinition CreateRecordNode(string id)
        {
            return new NodeDefinition
            {
                Id = id,
                Type = RecordingNodeFactory.TypeName,
                Name = id,
                Version = "1.0.0"
            };
        }

        private static EdgeDefinition CreateEdge(string fromNodeId, string fromPort, string toNodeId)
        {
            return new EdgeDefinition
            {
                FromNodeId = fromNodeId,
                FromPort = fromPort,
                ToNodeId = toNodeId,
                ToPort = "In"
            };
        }
    }
}
