using System;
using System.Collections.Generic;
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
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                return RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected test harness failure:");
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static async Task<int> RunAsync()
        {
            var tests = new List<TestCase>
            {
                new TestCase("FlowToken supports Set/Get/TryGet", FlowTokenTests.SetGetTryGet),
                new TestCase("Runtime serialization round-trips without view state", SerializationTests.RuntimeRoundTrip),
                new TestCase("Design serialization round-trips runtime and view state", SerializationTests.DesignRoundTrip),
                new TestCase("FlowValidator rejects duplicate NodeId", FlowValidationPublishTests.DuplicateNodeIdReturnsError),
                new TestCase("FlowValidator rejects dangling edges", FlowValidationPublishTests.DanglingEdgeReturnsError),
                new TestCase("FlowValidator rejects missing required settings", FlowValidationPublishTests.MissingRequiredSettingReturnsError),
                new TestCase("FlowValidator rejects missing binding outputs", FlowValidationPublishTests.MissingBindingOutputReturnsError),
                new TestCase("FlowValidator rejects invalid StreamFrames settings", FlowValidationPublishTests.InvalidStreamFramesSettingsReturnErrors),
                new TestCase("FlowValidator rejects invalid queue and group settings", FlowValidationPublishTests.InvalidQueueAndGroupSettingsReturnErrors),
                new TestCase("FlowPublishService removes designer view state", FlowValidationPublishTests.PublishRuntimeDoesNotContainViewState),
                new TestCase("FlowPublishService publishes a valid runtime", FlowValidationPublishTests.ValidFlowPublishesSuccessfully),
                new TestCase("Sample flow files deserialize and validate", SampleFlowTests.SampleFlowFilesDeserializeAndValidate),
                new TestCase("Continuous scan sample publishes runtime with enhanced rules", SampleFlowTests.ContinuousScanPublishesRuntimeWithEnhancedRules),
                new TestCase("Sample runtime file excludes designer view state", SampleFlowTests.SampleRuntimeExcludesViewState),
                new TestCase("FlowRunner executes A -> B -> C and writes output variables", FlowRunnerTests.LinearOrderAndVariables),
                new TestCase("FlowRunner executes all fan-out edges from one output port", FlowRunnerTests.FanOutExecutesAllOutgoingEdges),
                new TestCase("FlowRunner executes branched fan-out graph", FlowRunnerTests.BranchedFanOutGraphExecutesAllBranches),
                new TestCase("FlowRunner allows reconverging branches without global visited blocking", FlowRunnerTests.ReconvergingBranchesCanReachSameNode),
                new TestCase("FlowRunner publishes NodeFailed and follows Error route", FlowRunnerTests.NodeFailedAndErrorRoute),
                new TestCase("FlowRunner publishes NodeTimeout and follows Timeout route", FlowRunnerTests.NodeTimeoutAndTimeoutRoute),
                new TestCase("FlowRunner detects cycles on the current execution path", FlowRunnerTests.CycleRouteThrows),
                new TestCase("FlowRunner reports a clear missing entry exception", FlowRunnerTests.MissingEntryThrows),
                new TestCase("FlowRunner publishes runtime events in order", FlowRunnerTests.RuntimeEventOrder),
                new TestCase("FlowTaskQueue enforces capacity and publishes events", FlowTaskQueueTests.CapacityRejectsAndPublishesEvents),
                new TestCase("FlowTaskQueueRegistry reuses named queues", FlowTaskQueueTests.RegistryReusesNamedQueues),
                new TestCase("DefaultDeviceRegistry resolves a fake camera", AdapterTests.RegistryGetsFakeCamera),
                new TestCase("FakeCameraAdapter soft trigger raises FrameArrived", AdapterTests.SoftTriggerReceivesFrame),
                new TestCase("FakeCameraAdapter cancellation prevents frame creation", AdapterTests.SoftTriggerCancellationPreventsFrame),
                new TestCase("FakeCameraAdapter can return before frame arrives", AdapterTests.SoftTriggerCanReturnBeforeFrameArrived),
                new TestCase("VisionImageReference supports clone and disposal", AdapterTests.VisionImageReferenceLifecycle),
                new TestCase("FakeVisionImage supports clone and disposal", AdapterTests.FakeVisionImageLifecycle),
                new TestCase("FakeRecipeAdapter returns OK", AdapterTests.FakeRecipeReturnsOk),
                new TestCase("FakeImageSaveAdapter returns a simulated path", AdapterTests.FakeImageSaveReturnsPath),
                new TestCase("FakeImageSaveAdapter snapshots image references", AdapterTests.FakeImageSaveSnapshotsImageReference),
                new TestCase("CommonNodeRegistration resolves common factories", CommonNodeTests.RegisterAllResolvesFactories),
                new TestCase("LogNode publishes a runtime log event", CommonNodeTests.LogNodePublishesRuntimeEvent),
                new TestCase("DelayNode executes a configured delay", CommonNodeTests.DelayNodeExecutes),
                new TestCase("VariableSetNode writes a variable subsequent node can read", CommonNodeTests.VariableSetNodeWritesVariableForNextNode),
                new TestCase("AndJoinNode triggers after two inputs with the same JoinKey", ControlFlowNodeTests.AndJoinTwoInputsSameJoinKey),
                new TestCase("AndJoinNode keeps different JoinKeys isolated", ControlFlowNodeTests.AndJoinDifferentKeysDoNotMix),
                new TestCase("AndJoinNode duplicate policy Error routes to Error", ControlFlowNodeTests.AndJoinDuplicatePolicyError),
                new TestCase("ConditionNode routes true and false branches", ControlFlowNodeTests.ConditionTrueFalseRoutes),
                new TestCase("MotionNotifyNode sends a fake motion message", MotionNodeTests.MotionNotifyWithFakeMotion),
                new TestCase("MotionMoveToNode moves fake motion", MotionNodeTests.MotionMoveToWithFakeMotion),
                new TestCase("MotionWaitInPositionNode waits fake motion", MotionNodeTests.MotionWaitInPositionWithFakeMotion),
                new TestCase("Motion node missing MotionId routes to Error", MotionNodeTests.MissingMotionIdRoutesError),
                new TestCase("Camera nodes set parameters, trigger, and receive a matching frame", CameraNodeTests.SetTriggerCallbackFlow),
                new TestCase("CameraImageCallbackNode times out on mismatched TriggerId", CameraNodeTests.ImageCallbackTimeoutWhenTriggerIdDoesNotMatch),
                new TestCase("CameraImageCallbackNode can match any next frame", CameraNodeTests.ImageCallbackAnyMatchMode),
                new TestCase("CameraImageCallbackNode stream mode collects frames", CameraNodeTests.ImageCallbackStreamFrames),
                new TestCase("Stage 07 nodes run callback recipe save and database chain", Stage07NodeTests.CallbackRecipeSaveDatabaseFlow),
                new TestCase("Stage 07 recipe save and database nodes can run through queues", Stage07NodeTests.QueuedRecipeSaveDatabaseFlow),
                new TestCase("Stage 08 FrameGroupJoin completes, sorts frames, and stitches", Stage08NodeTests.FrameGroupJoinSortsAndStitches),
                new TestCase("Stage 08 FrameGroupJoin detects duplicate ShotIndex", Stage08NodeTests.FrameGroupJoinDetectsDuplicateShotIndex),
                new TestCase("Stage 08 FrameGroupJoin supports bindings replace duplicates and continuous validation", Stage08NodeTests.FrameGroupJoinBindingsReplaceAndContinuousValidation),
                new TestCase("Stage 08 FrameGroupJoin detects non-continuous ShotIndex", Stage08NodeTests.FrameGroupJoinDetectsNonContinuousShotIndex),
                new TestCase("Stage 08 ScanGroupJoin sorts preprocess results and fusion outputs images", Stage08NodeTests.ScanGroupJoinSortsAndFusionOutputsImages),
                new TestCase("Stage 08 ScanGroupJoin supports bindings replace duplicates and fusion binding", Stage08NodeTests.ScanGroupJoinBindingsReplaceAndFusionBinding),
                new TestCase("Stage 08 ScanGroupJoin detects non-continuous FrameIndex", Stage08NodeTests.ScanGroupJoinDetectsNonContinuousFrameIndex)
            };

            var failed = 0;
            foreach (var test in tests)
            {
                try
                {
                    await test.RunAsync().ConfigureAwait(false);
                    Console.WriteLine("[PASS] " + test.Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("[FAIL] " + test.Name);
                    Console.WriteLine(ex);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Tests run: " + tests.Count + ", Failed: " + failed);
            return failed == 0 ? 0 : 1;
        }
    }

    internal sealed class TestCase
    {
        private readonly Func<Task> _runAsync;

        public TestCase(string name, Func<Task> runAsync)
        {
            Name = name;
            _runAsync = runAsync;
        }

        public string Name { get; private set; }

        public Task RunAsync()
        {
            return _runAsync();
        }
    }

    internal static class CommonNodeTests
    {
        public static Task RegisterAllResolvesFactories()
        {
            var registry = new NodeRegistry();

            CommonNodeRegistration.RegisterAll(registry);

            AssertFactoryRegistered(registry, LogNodeFactory.TypeName);
            AssertFactoryRegistered(registry, DelayNodeFactory.TypeName);
            AssertFactoryRegistered(registry, SplitNodeFactory.TypeName);
            AssertFactoryRegistered(registry, VariableSetNodeFactory.TypeName);
            AssertFactoryRegistered(registry, AndJoinNodeFactory.TypeName);
            AssertFactoryRegistered(registry, ConditionNodeFactory.TypeName);
            AssertFactoryRegistered(registry, MotionNotifyNodeFactory.TypeName);
            AssertFactoryRegistered(registry, MotionMoveToNodeFactory.TypeName);
            AssertFactoryRegistered(registry, MotionWaitInPositionNodeFactory.TypeName);
            AssertFactoryRegistered(registry, CameraSetParameterNodeFactory.TypeName);
            AssertFactoryRegistered(registry, CameraSoftTriggerNodeFactory.TypeName);
            AssertFactoryRegistered(registry, CameraImageCallbackNodeFactory.TypeName);
            AssertFactoryRegistered(registry, LightControlNodeFactory.TypeName);
            AssertFactoryRegistered(registry, RecipeRunNodeFactory.TypeName);
            AssertFactoryRegistered(registry, ImageSaveNodeFactory.TypeName);
            AssertFactoryRegistered(registry, DatabaseSaveNodeFactory.TypeName);
            AssertFactoryRegistered(registry, FrameGroupJoinNodeFactory.TypeName);
            AssertFactoryRegistered(registry, StitchNodeFactory.TypeName);
            AssertFactoryRegistered(registry, FramePreprocessNodeFactory.TypeName);
            AssertFactoryRegistered(registry, ScanGroupJoinNodeFactory.TypeName);
            AssertFactoryRegistered(registry, Final3D2DFusionNodeFactory.TypeName);
            return Task.FromResult(0);
        }

        public static async Task LogNodePublishesRuntimeEvent()
        {
            var sink = new InMemoryFlowEventSink();
            var runner = CreateCommonRunner(CreateSingleCommonNodeFlow(
                "log1",
                LogNodeFactory.TypeName,
                delegate(NodeDefinition node)
                {
                    node.Settings["Level"] = "Info";
                    node.Settings["Message"] = "Part reached station.";
                }),
                sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-log" }).ConfigureAwait(false);

            var logEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeCompleted &&
                string.Equals(x.NodeId, "log1", StringComparison.OrdinalIgnoreCase) &&
                x.Data.ContainsKey("Kind") &&
                string.Equals(Convert.ToString(x.Data["Kind"]), "Log", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(logEvent, "LogNode should publish a runtime event marked as a log.");
            AssertEx.Equal("Part reached station.", logEvent.Message, "Log event message should match the configured message.");
            AssertEx.Equal("Info", Convert.ToString(logEvent.Data["LogLevel"]), "Log event should include the configured log level.");
        }

        public static async Task DelayNodeExecutes()
        {
            var sink = new InMemoryFlowEventSink();
            var runner = CreateCommonRunner(CreateSingleCommonNodeFlow(
                "delay1",
                DelayNodeFactory.TypeName,
                delegate(NodeDefinition node)
                {
                    node.Settings["DelayMs"] = 1;
                }),
                sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-delay" }).ConfigureAwait(false);

            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.NodeCompleted &&
                    string.Equals(x.NodeId, "delay1", StringComparison.OrdinalIgnoreCase) &&
                    x.OutputPort == "Next"),
                "DelayNode should complete through the Next port.");
            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.OutputProduced &&
                    string.Equals(Convert.ToString(x.Data["VariableName"]), "delay1.DelayMs", StringComparison.OrdinalIgnoreCase) &&
                    object.Equals(1, x.Data["Value"])),
                "DelayNode should publish the resolved DelayMs output.");
        }

        public static async Task VariableSetNodeWritesVariableForNextNode()
        {
            var sink = new InMemoryFlowEventSink();
            var executionLog = new List<string>();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new RecordingNodeFactory(executionLog));

            var flow = new RuntimeFlowDefinition
            {
                FlowId = "variable-set",
                FlowName = "Variable Set",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "set1",
                Type = VariableSetNodeFactory.TypeName,
                Name = "Set Shared Variable",
                Version = "1.0.0",
                Settings =
                {
                    { "VariableName", "Shared.Value" },
                    { "Value", "station-ok" }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "reader1",
                Type = RecordingNodeFactory.TypeName,
                Name = "Read Shared Variable",
                Version = "1.0.0",
                Settings =
                {
                    { "RequiredVariable", "Shared.Value" }
                }
            });

            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "set1",
                FromPort = "Next",
                ToNodeId = "reader1",
                ToPort = "In"
            });
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "set1" });

            var runner = new FlowEngine(registry, sink).CreateRunner(flow);
            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-variable" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "reader1" }, executionLog, "Subsequent node should execute after VariableSetNode.");
            AssertEx.False(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeFailed),
                "VariableSetNode should write the named variable before the next node reads it.");
            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.OutputProduced &&
                    string.Equals(Convert.ToString(x.Data["VariableName"]), "set1.Value", StringComparison.OrdinalIgnoreCase) &&
                    object.Equals("station-ok", x.Data["Value"])),
                "VariableSetNode should also publish the written value as a node output.");
        }

        private static void AssertFactoryRegistered(NodeRegistry registry, string nodeType)
        {
            INodeFactory factory;
            AssertEx.True(registry.TryGetFactory(nodeType, out factory), nodeType + " factory should be registered.");
            AssertEx.NotNull(factory.Descriptor, nodeType + " descriptor should be available.");
            AssertEx.Equal(nodeType, factory.Descriptor.NodeType, nodeType + " descriptor should use the registered node type.");
        }

        private static IFlowRunner CreateCommonRunner(RuntimeFlowDefinition flow, InMemoryFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateSingleCommonNodeFlow(string nodeId, string nodeType, Action<NodeDefinition> configure)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = nodeId + "-flow",
                FlowName = nodeId + " Flow",
                Version = "1.0.0"
            };

            var node = new NodeDefinition
            {
                Id = nodeId,
                Type = nodeType,
                Name = nodeId,
                Version = "1.0.0"
            };

            if (configure != null)
            {
                configure(node);
            }

            flow.Nodes.Add(node);
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = nodeId });
            return flow;
        }
    }

    internal static class ControlFlowNodeTests
    {
        public static async Task AndJoinTwoInputsSameJoinKey()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateAndJoinFlow("Ignore", includeErrorHandler: false), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-1", PositionId = "P01" }).ConfigureAwait(false);
            AssertEx.Equal(0, executionLog.Count, "First input should wait for another token with the same JoinKey.");

            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-2", PositionId = "P01" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "Done" }, executionLog, "Second input with the same JoinKey should complete the join.");
            AssertEx.Equal(true, FindLastOutput(sink, "join1", "Result"), "Completed join should output Result=true.");
            AssertEx.Equal(true, FindLastOutput(sink, "join1", "IsMatched"), "Completed join should output IsMatched=true.");
        }

        public static async Task AndJoinDifferentKeysDoNotMix()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateAndJoinFlow("Ignore", includeErrorHandler: false), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-a", PositionId = "P01" }).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "join-token-b", PositionId = "P02" }).ConfigureAwait(false);

            AssertEx.Equal(0, executionLog.Count, "Different JoinKeys should remain in separate waiting buckets.");
            AssertEx.Equal(2, CountOutputValues(sink, "join1", "IsMatched", false), "Both different keys should report waiting outputs.");
        }

        public static async Task AndJoinDuplicatePolicyError()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateAndJoinFlow("Error", includeErrorHandler: true), executionLog, sink);
            var token = new FlowToken { TokenId = "duplicate-token", PositionId = "P01" };

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "ErrorHandler" }, executionLog, "DuplicatePolicy=Error should route duplicate inputs through Error.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeFailed && string.Equals(x.NodeId, "join1", StringComparison.OrdinalIgnoreCase)),
                "Duplicate join input should publish NodeFailed.");
        }

        public static async Task ConditionTrueFalseRoutes()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateConditionFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "condition-token-true", PositionId = "P01" }).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "condition-token-false", PositionId = "P02" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "TrueNode", "FalseNode" }, executionLog, "ConditionNode should route matching and non-matching tokens.");
            AssertEx.Equal(1, CountOutputValues(sink, "condition1", "IsMatched", true), "True branch should produce IsMatched=true once.");
            AssertEx.Equal(1, CountOutputValues(sink, "condition1", "IsMatched", false), "False branch should produce IsMatched=false once.");
        }

        private static IFlowRunner CreateRunner(RuntimeFlowDefinition flow, IList<string> executionLog, InMemoryFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            registry.Register(new RecordingNodeFactory(executionLog));
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateAndJoinFlow(string duplicatePolicy, bool includeErrorHandler)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "and-join",
                FlowName = "AND Join",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = AndJoinNodeFactory.TypeName,
                Name = "Join",
                Version = "1.0.0",
                Settings =
                {
                    { "JoinKeyBinding", "{{ token.PositionId }}" },
                    { "ExpectedInputCount", 2 },
                    { "TimeoutMs", 0 },
                    { "DuplicatePolicy", duplicatePolicy }
                }
            });
            flow.Nodes.Add(CreateRecordNode("Done"));
            flow.Edges.Add(CreateEdge("join1", "Next", "Done"));

            if (includeErrorHandler)
            {
                flow.Nodes.Add(CreateRecordNode("ErrorHandler"));
                flow.Edges.Add(CreateEdge("join1", "Error", "ErrorHandler"));
            }

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateConditionFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "condition",
                FlowName = "Condition",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "condition1",
                Type = ConditionNodeFactory.TypeName,
                Name = "Condition",
                Version = "1.0.0",
                Settings =
                {
                    { "LeftBinding", "{{ token.PositionId }}" },
                    { "Operator", "Equal" },
                    { "RightValue", "P01" }
                }
            });
            flow.Nodes.Add(CreateRecordNode("TrueNode"));
            flow.Nodes.Add(CreateRecordNode("FalseNode"));
            flow.Edges.Add(CreateEdge("condition1", "True", "TrueNode"));
            flow.Edges.Add(CreateEdge("condition1", "False", "FalseNode"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "condition1" });
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

        private static object FindLastOutput(InMemoryFlowEventSink sink, string nodeId, string outputName)
        {
            var variableName = nodeId + "." + outputName;
            var runtimeEvent = sink.Events.LastOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data["VariableName"]), variableName, StringComparison.OrdinalIgnoreCase));
            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data["Value"];
        }

        private static int CountOutputValues(InMemoryFlowEventSink sink, string nodeId, string outputName, object expectedValue)
        {
            var variableName = nodeId + "." + outputName;
            return sink.Events.Count(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data["VariableName"]), variableName, StringComparison.OrdinalIgnoreCase) &&
                object.Equals(x.Data["Value"], expectedValue));
        }
    }

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

    internal static class CameraNodeTests
    {
        public static async Task SetTriggerCallbackFlow()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 20
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var runner = CreateCameraRunner(CreateSetTriggerCallbackFlow(), sink, devices, null);
            var token = new FlowToken { TokenId = "token-camera-chain" };

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", token).ConfigureAwait(false);

            var exposure = await camera.GetParameterAsync("ExposureTime", CancellationToken.None).ConfigureAwait(false);
            var gain = await camera.GetParameterAsync("Gain", CancellationToken.None).ConfigureAwait(false);
            AssertEx.Equal(1234.5, Convert.ToDouble(exposure), "CameraSetParameterNode should set ExposureTime.");
            AssertEx.Equal(2.5, Convert.ToDouble(gain), "CameraSetParameterNode should set Gain.");

            var triggerId = Convert.ToString(FindOutput(sink, "trigger1", "TriggerId"));
            var frame = FindOutput(sink, "callback1", "Frame") as CameraFrameData;
            var image = FindOutput(sink, "callback1", "Image") as IVisionImage;
            var frameId = Convert.ToString(FindOutput(sink, "callback1", "FrameId"));
            var metadata = FindOutput(sink, "callback1", "Metadata") as IDictionary<string, object>;

            AssertEx.NotNull(frame, "CameraImageCallbackNode should output the matched frame.");
            AssertEx.NotNull(image, "CameraImageCallbackNode should output the matched image.");
            AssertEx.NotNull(metadata, "CameraImageCallbackNode should output frame metadata.");
            AssertEx.Equal("Camera01", frame.CameraId, "Callback frame camera id should match the configured camera.");
            AssertEx.Equal(triggerId, frame.TriggerId, "Callback frame should match the soft trigger id.");
            AssertEx.Equal(frame.FrameId, frameId, "FrameId output should match the frame.");
            AssertEx.Equal(frame.FrameId, image.ImageId, "Fake image id should match the frame id.");
            AssertEx.Equal(triggerId, Convert.ToString(metadata["TriggerId"]), "Frame metadata should carry the trigger id.");
            AssertEx.Equal(triggerId, token.Get<string>("TriggerId"), "Soft trigger should write TriggerId to the token.");
            AssertEx.Equal(frame.FrameId, token.FrameId, "Image callback should write FrameId to the token.");
        }

        public static async Task ImageCallbackTimeoutWhenTriggerIdDoesNotMatch()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var executionLog = new List<string>();
            var runner = CreateCameraRunner(CreateMismatchedTriggerFlow(), sink, devices, executionLog);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-camera-timeout" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "TimeoutHandler" }, executionLog, "Mismatched TriggerId should route through the Timeout output.");
            AssertEx.True(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.NodeTimeout &&
                    string.Equals(x.NodeId, "callback1", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.OutputPort, "Timeout", StringComparison.OrdinalIgnoreCase)),
                "CameraImageCallbackNode should publish a NodeTimeout event.");
            AssertEx.False(
                sink.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.OutputProduced &&
                    string.Equals(Convert.ToString(x.Data["VariableName"]), "callback1.Image", StringComparison.OrdinalIgnoreCase)),
                "Timed out callback should not publish image outputs.");
        }

        public static async Task ImageCallbackAnyMatchMode()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var runner = CreateCameraRunner(CreateAnyMatchFlow(), sink, devices, null);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-camera-any" }).ConfigureAwait(false);

            var triggerId = Convert.ToString(FindOutput(sink, "trigger1", "TriggerId"));
            var frame = FindOutput(sink, "callback1", "Frame") as CameraFrameData;

            AssertEx.NotNull(frame, "Any match mode should output the first available frame.");
            AssertEx.Equal("Camera01", frame.CameraId, "Any match frame camera id should match.");
            AssertEx.Equal(triggerId, frame.TriggerId, "Any match should consume the soft trigger frame.");
        }

        public static async Task ImageCallbackStreamFrames()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);

            var sink = new InMemoryFlowEventSink();
            var runner = CreateCameraRunner(CreateStreamFramesFlow(), sink, devices, null);

            await runner.StartAsync().ConfigureAwait(false);
            var triggerTask = runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-camera-stream" });

            await Task.Delay(20).ConfigureAwait(false);
            await camera.SoftTriggerAsync(
                new CameraTriggerContext { CameraId = "Camera01", TriggerId = "stream-001" },
                CancellationToken.None).ConfigureAwait(false);
            await camera.SoftTriggerAsync(
                new CameraTriggerContext { CameraId = "Camera01", TriggerId = "stream-002" },
                CancellationToken.None).ConfigureAwait(false);

            await triggerTask.ConfigureAwait(false);

            var frames = FindOutput(sink, "callback1", "Frames") as IList<CameraFrameData>;
            var frameCount = Convert.ToInt32(FindOutput(sink, "callback1", "FrameCount"), CultureInfo.InvariantCulture);
            var lastTriggerId = Convert.ToString(FindOutput(sink, "callback1", "TriggerId"));

            AssertEx.NotNull(frames, "StreamFrames should output the collected frame list.");
            AssertEx.Equal(2, frames.Count, "StreamFrames should collect the configured frame count.");
            AssertEx.Equal(2, frameCount, "FrameCount output should match the collected frame count.");
            AssertEx.Equal("stream-001", frames[0].TriggerId, "First stream frame should preserve trigger id.");
            AssertEx.Equal("stream-002", frames[1].TriggerId, "Second stream frame should preserve trigger id.");
            AssertEx.Equal("stream-002", lastTriggerId, "Node scalar outputs should describe the last collected frame.");
        }

        private static IFlowRunner CreateCameraRunner(
            RuntimeFlowDefinition flow,
            InMemoryFlowEventSink sink,
            DefaultDeviceRegistry devices,
            IList<string> executionLog)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            if (executionLog != null)
            {
                registry.Register(new RecordingNodeFactory(executionLog));
            }

            return new FlowEngine(registry, sink, devices).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateSetTriggerCallbackFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-chain",
                FlowName = "Camera Chain",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "set1",
                Type = CameraSetParameterNodeFactory.TypeName,
                Name = "Set Camera Parameters",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 },
                    {
                        "Parameters",
                        new[]
                        {
                            new CameraParameterSetConfig { Name = "ExposureTime", Value = 1234.5 },
                            new CameraParameterSetConfig { Name = "Gain", Value = 2.5 }
                        }
                    }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "MatchMode", "TriggerId" },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "TriggerId", VariableBinding.ForVariable("trigger1", "TriggerId") }
                }
            });

            flow.Edges.Add(CreateEdge("set1", "Next", "trigger1"));
            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "set1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateAnyMatchFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-any",
                FlowName = "Camera Any Match",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "CallbackMode", "WaitNextFrame" },
                    { "MatchMode", "Any" },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "trigger1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateStreamFramesFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-stream",
                FlowName = "Camera Stream",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "CallbackMode", "StreamFrames" },
                    { "MatchMode", "Any" },
                    { "ExpectedFrameCount", 2 },
                    { "FrameTimeoutMs", 1000 },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "callback1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateMismatchedTriggerFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "camera-timeout",
                FlowName = "Camera Timeout",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TriggerId", "missing-trigger-id" },
                    { "MatchMode", "TriggerId" },
                    { "TimeoutMs", 50 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "TimeoutHandler",
                Type = RecordingNodeFactory.TypeName,
                Name = "Timeout Handler",
                Version = "1.0.0"
            });

            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Edges.Add(CreateEdge("callback1", "Timeout", "TimeoutHandler"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "trigger1" });
            return flow;
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

        private static object FindOutput(InMemoryFlowEventSink sink, string nodeId, string outputName)
        {
            var variableName = nodeId + "." + outputName;
            var runtimeEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data["VariableName"]), variableName, StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data["Value"];
        }
    }

    internal static class Stage07NodeTests
    {
        public static async Task CallbackRecipeSaveDatabaseFlow()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var light = new FakeLightAdapter("Light01");
            var recipe = new FakeRecipeAdapter("Recipe01");
            var resultImage = new FakeVisionImage("result-image-001", 640, 480, "RGB24", null);
            recipe.DefaultOutputs["ResultImage"] = resultImage;
            recipe.DefaultOutputs["IsOk"] = true;

            var saver = new FakeImageSaveAdapter("ImageSave01");
            var database = new FakeDatabaseAdapter("VisionDb");
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);
            devices.RegisterLight(light);
            devices.RegisterRecipe(recipe);
            devices.RegisterImageSaver(saver);
            devices.RegisterDatabase(database);

            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink, devices).CreateRunner(CreateStage07Flow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync(
                "ManualStart",
                new FlowToken
                {
                    TokenId = "token-stage07",
                    ProductId = "P-007",
                    WorkpieceId = "W-007"
                }).ConfigureAwait(false);

            var lightSnapshot = light.Snapshot();
            AssertEx.True(lightSnapshot.ContainsKey("CH1"), "LightControlNode should set CH1.");
            AssertEx.True(lightSnapshot["CH1"].IsEnabled, "LightControlNode should enable CH1.");
            AssertEx.Equal(75.0, lightSnapshot["CH1"].Intensity, "LightControlNode should set CH1 intensity.");

            var callbackImage = FindOutput(sink, "callback1", "Image") as IVisionImage;
            var frameId = Convert.ToString(FindOutput(sink, "callback1", "FrameId"));
            var recipeResult = FindOutput(sink, "recipe1", "Result") as RecipeRunResult;
            var isOk = Convert.ToBoolean(FindOutput(sink, "recipe1", "IsOk"));
            var recipeResultImage = FindOutput(sink, "recipe1", "ResultImage") as IVisionImage;

            AssertEx.NotNull(callbackImage, "Camera callback should output an image for the stage 07 chain.");
            AssertEx.NotNull(recipeResult, "RecipeRunNode should output the adapter result.");
            AssertEx.True(isOk, "RecipeRunNode should output IsOk from the fake recipe.");
            AssertEx.True(object.ReferenceEquals(resultImage, recipeResultImage), "RecipeRunNode should output the fake recipe result image.");
            AssertEx.True(object.ReferenceEquals(callbackImage, recipeResult.Outputs["Input.InputImage"]), "RecipeRunNode should pass InputImage through variable binding.");

            var imagePath = Convert.ToString(FindOutput(sink, "save1", "ImagePath"));
            var resultImagePath = Convert.ToString(FindOutput(sink, "save1", "ResultImagePath"));
            var expectedDirectory = "fake://images/Camera01/OK";
            AssertEx.Equal(expectedDirectory + "/" + frameId + ".png", imagePath, "ImageSaveNode should output the raw image save path.");
            AssertEx.Equal(expectedDirectory + "/" + frameId + "_result.png", resultImagePath, "ImageSaveNode should output the result image save path.");

            var savedImages = saver.SnapshotSavedRequests();
            AssertEx.Equal(2, savedImages.Count, "ImageSaveNode should call the image saver for raw and result images.");
            AssertEx.Equal(expectedDirectory, savedImages[0].DirectoryPath, "Raw image save request should use the rendered directory.");
            AssertEx.Equal(frameId + ".png", savedImages[0].FileName, "Raw image save request should use the rendered file name.");
            AssertEx.Equal("Image", Convert.ToString(savedImages[0].Metadata["Role"]), "Raw image save request should be marked as Image.");
            AssertEx.Equal(frameId + "_result.png", savedImages[1].FileName, "Result image save request should use a result file name.");
            AssertEx.Equal("ResultImage", Convert.ToString(savedImages[1].Metadata["Role"]), "Result image save request should be marked as ResultImage.");

            var dbSaved = Convert.ToBoolean(FindOutput(sink, "db1", "Saved"));
            var savedRows = database.SnapshotSavedRequests();
            AssertEx.True(dbSaved, "DatabaseSaveNode should output Saved=true.");
            AssertEx.Equal(1, savedRows.Count, "DatabaseSaveNode should call the database adapter once.");
            AssertEx.Equal("InspectionResult", savedRows[0].TableName, "DatabaseSaveNode should use the configured table.");
            AssertEx.Equal(frameId, Convert.ToString(savedRows[0].Values["FrameId"]), "DatabaseSaveNode should save the bound FrameId.");
            AssertEx.Equal(imagePath, Convert.ToString(savedRows[0].Values["ImagePath"]), "DatabaseSaveNode should save the bound ImagePath.");
            AssertEx.Equal(resultImagePath, Convert.ToString(savedRows[0].Values["ResultImagePath"]), "DatabaseSaveNode should save the bound ResultImagePath.");
            AssertEx.True(Convert.ToBoolean(savedRows[0].Values["IsOk"]), "DatabaseSaveNode should save the bound IsOk value.");
        }

        public static async Task QueuedRecipeSaveDatabaseFlow()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var light = new FakeLightAdapter("Light01");
            var recipe = new FakeRecipeAdapter("Recipe01");
            recipe.DefaultOutputs["ResultImage"] = new FakeVisionImage("result-image-queued", 640, 480, "RGB24", null);
            recipe.DefaultOutputs["IsOk"] = true;

            var saver = new FakeImageSaveAdapter("ImageSave01");
            var database = new FakeDatabaseAdapter("VisionDb");
            var devices = new DefaultDeviceRegistry();
            devices.RegisterCamera(camera);
            devices.RegisterLight(light);
            devices.RegisterRecipe(recipe);
            devices.RegisterImageSaver(saver);
            devices.RegisterDatabase(database);

            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink, devices).CreateRunner(CreateQueuedStage07Flow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync(
                "ManualStart",
                new FlowToken
                {
                    TokenId = "token-stage07-queued",
                    ProductId = "P-007",
                    WorkpieceId = "W-007"
                }).ConfigureAwait(false);

            AssertQueueCompleted(sink, "recipe1", "recipe", "recipe.run", 1);
            AssertQueueCompleted(sink, "save1", "image-save", "image.save.Image", 1);
            AssertQueueCompleted(sink, "save1", "image-save", "image.save.ResultImage", 1);
            AssertQueueCompleted(sink, "db1", "database-save", "database.save", 1);

            AssertEx.NotNull(FindOutput(sink, "recipe1", "Result"), "Queued RecipeRunNode should still output Result.");
            AssertEx.NotNull(FindOutput(sink, "save1", "ImagePath"), "Queued ImageSaveNode should still output ImagePath.");
            AssertEx.True(Convert.ToBoolean(FindOutput(sink, "db1", "Saved")), "Queued DatabaseSaveNode should still output Saved=true.");
            AssertEx.Equal(2, saver.SnapshotSavedRequests().Count, "Queued ImageSaveNode should save raw and result images.");
            AssertEx.Equal(1, database.SnapshotSavedRequests().Count, "Queued DatabaseSaveNode should save one row.");
        }

        private static RuntimeFlowDefinition CreateStage07Flow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage07-chain",
                FlowName = "Stage 07 Chain",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "light1",
                Type = LightControlNodeFactory.TypeName,
                Name = "Light Control",
                Version = "1.0.0",
                Settings =
                {
                    { "LightId", "Light01" },
                    { "StableDelayMs", 1 },
                    {
                        "Channels",
                        new[]
                        {
                            new LightChannelControlConfig
                            {
                                ChannelName = "CH1",
                                IsEnabled = true,
                                Intensity = 75.0,
                                DurationMs = 10
                            }
                        }
                    }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "trigger1",
                Type = CameraSoftTriggerNodeFactory.TypeName,
                Name = "Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 500 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "callback1",
                Type = CameraImageCallbackNodeFactory.TypeName,
                Name = "Image Callback",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "MatchMode", "TriggerId" },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "TriggerId", VariableBinding.ForVariable("trigger1", "TriggerId") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "recipe1",
                Type = RecipeRunNodeFactory.TypeName,
                Name = "Recipe Run",
                Version = "1.0.0",
                Settings =
                {
                    { "RecipeId", "Recipe01" },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "InputImage", VariableBinding.ForVariable("callback1", "Image") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "save1",
                Type = ImageSaveNodeFactory.TypeName,
                Name = "Image Save",
                Version = "1.0.0",
                Settings =
                {
                    { "SaverId", "ImageSave01" },
                    { "RootDirectory", "fake://images" },
                    { "DirectoryTemplate", "{CameraId}/{Result}" },
                    { "FileNameTemplate", "{FrameId}.png" }
                },
                InputBindings =
                {
                    { "Image", VariableBinding.ForVariable("callback1", "Image") },
                    { "ResultImage", VariableBinding.ForVariable("recipe1", "ResultImage") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "db1",
                Type = DatabaseSaveNodeFactory.TypeName,
                Name = "Database Save",
                Version = "1.0.0",
                Settings =
                {
                    { "DatabaseId", "VisionDb" },
                    { "TableName", "InspectionResult" },
                    {
                        "FieldMappings",
                        new[]
                        {
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "FrameId",
                                ValueBinding = "{{ callback1.FrameId }}"
                            },
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "ImagePath",
                                ValueBinding = "{{ save1.ImagePath }}"
                            },
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "ResultImagePath",
                                ValueBinding = "{{ save1.ResultImagePath }}"
                            },
                            new DatabaseFieldMappingConfig
                            {
                                FieldName = "IsOk",
                                ValueBinding = "{{ recipe1.IsOk }}"
                            }
                        }
                    }
                }
            });

            flow.Edges.Add(CreateEdge("light1", "Next", "trigger1"));
            flow.Edges.Add(CreateEdge("trigger1", "Next", "callback1"));
            flow.Edges.Add(CreateEdge("callback1", "Next", "recipe1"));
            flow.Edges.Add(CreateEdge("recipe1", "Next", "save1"));
            flow.Edges.Add(CreateEdge("save1", "Next", "db1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "light1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateQueuedStage07Flow()
        {
            var flow = CreateStage07Flow();
            EnableQueue(flow, "recipe1", "recipe");
            EnableQueue(flow, "save1", "image-save");
            EnableQueue(flow, "db1", "database-save");
            return flow;
        }

        private static void EnableQueue(RuntimeFlowDefinition flow, string nodeId, string queueName)
        {
            var node = flow.Nodes.First(x => string.Equals(x.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            node.Settings["UseQueue"] = true;
            node.Settings["QueueName"] = queueName;
            node.Settings["QueueCapacity"] = 8;
            node.Settings["QueueMaxDegreeOfParallelism"] = 1;
            node.Settings["QueueFullMode"] = "Reject";
        }

        private static void AssertQueueCompleted(
            InMemoryFlowEventSink sink,
            string nodeId,
            string queueName,
            string operationName,
            int expectedCount)
        {
            var count = sink.Events.Count(x =>
                x.EventType == FlowRuntimeEventType.QueueCompleted &&
                string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Convert.ToString(x.Data["QueueName"], CultureInfo.InvariantCulture), queueName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Convert.ToString(x.Data["OperationName"], CultureInfo.InvariantCulture), operationName, StringComparison.OrdinalIgnoreCase));

            AssertEx.Equal(expectedCount, count, "QueueCompleted event count should match for " + nodeId + " / " + operationName + ".");
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

        private static object FindOutput(InMemoryFlowEventSink sink, string nodeId, string outputName)
        {
            var variableName = nodeId + "." + outputName;
            var runtimeEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data["VariableName"]), variableName, StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data["Value"];
        }
    }

    internal static class Stage08NodeTests
    {
        public static async Task FrameGroupJoinSortsAndStitches()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateFrameGroupStitchFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-A", 2)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-A", 1)).ConfigureAwait(false);

            var group = FindOutput(sink, "join1", "FrameGroup") as FrameGroupResult;
            var stitched = FindOutput(sink, "stitch1", "StitchedImage") as IVisionImage;

            AssertEx.NotNull(group, "FrameGroupJoinNode should output a completed frame group.");
            AssertEx.Equal("capture-A", group.CaptureGroupId, "FrameGroupResult should keep the capture group id.");
            AssertEx.Equal(2, group.ActualShotCount, "FrameGroupResult should include both frames.");
            AssertEx.SequenceEqual(new[] { 1, 2 }, group.Frames.Select(x => x.ShotIndex), "FrameGroupResult should be sorted by ShotIndex.");
            AssertEx.NotNull(stitched, "StitchNode should output a stitched image.");
            AssertEx.Equal("capture-A", Convert.ToString(stitched.Metadata["CaptureGroupId"]), "Stitched image should carry CaptureGroupId metadata.");
            AssertEx.Equal(2, Convert.ToInt32(stitched.Metadata["SourceFrameCount"]), "Stitched image should record source frame count.");
        }

        public static async Task FrameGroupJoinDetectsDuplicateShotIndex()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateDuplicateFrameGroupFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-duplicate", 1)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-duplicate", 1)).ConfigureAwait(false);

            var failure = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeFailed &&
                string.Equals(x.NodeId, "join1", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(failure, "Duplicate ShotIndex should fail FrameGroupJoinNode.");
            AssertEx.True(
                failure.Message.IndexOf("Duplicate ShotIndex", StringComparison.OrdinalIgnoreCase) >= 0,
                "FrameGroupJoinNode failure should identify the duplicate ShotIndex.");
        }

        public static async Task FrameGroupJoinBindingsReplaceAndContinuousValidation()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateFrameGroupBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-bind", 1)).ConfigureAwait(false);

            var replacement = CreateCaptureToken("capture-bind", 1);
            var replacementFrame = CreateFrame("capture-bind", 1);
            replacementFrame.FrameId = "replacement-shot-1";
            replacementFrame.Image = new FakeVisionImage("replacement-shot-1", 111, 80, "Mono8", null);
            replacement.FrameId = replacementFrame.FrameId;
            replacement.Set("Frame", replacementFrame);
            await runner.TriggerAsync("ManualStart", replacement).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-bind", 2)).ConfigureAwait(false);

            var group = FindOutput(sink, "join1", "FrameGroup") as FrameGroupResult;
            AssertEx.NotNull(group, "FrameGroupJoinNode should complete after a replaced duplicate and the second shot.");
            AssertEx.SequenceEqual(new[] { 1, 2 }, group.Frames.Select(x => x.ShotIndex), "FrameGroupJoinNode should keep continuous ShotIndexes from settings.");
            AssertEx.Equal("replacement-shot-1", group.Frames[0].FrameId, "DuplicatePolicy=Replace should keep the latest duplicate item.");
        }

        public static async Task FrameGroupJoinDetectsNonContinuousShotIndex()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateFrameGroupBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-gap", 1)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateCaptureToken("capture-gap", 3)).ConfigureAwait(false);

            var failure = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeFailed &&
                string.Equals(x.NodeId, "join1", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(failure, "Non-continuous ShotIndex values should fail FrameGroupJoinNode when validation is enabled.");
            AssertEx.True(
                failure.Message.IndexOf("continuous", StringComparison.OrdinalIgnoreCase) >= 0,
                "FrameGroupJoinNode failure should identify continuous ShotIndex validation.");
        }

        public static async Task ScanGroupJoinSortsAndFusionOutputsImages()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateScanFusionFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-A", 2)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-A", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-A", 1)).ConfigureAwait(false);

            var scanGroup = FindOutput(sink, "scanJoin1", "ScanGroupResult") as ScanGroupResult;
            var final3D = FindOutput(sink, "fusion1", "Final3DImage") as IVisionImage;
            var final2D = FindOutput(sink, "fusion1", "Final2DImage") as IVisionImage;

            AssertEx.NotNull(scanGroup, "ScanGroupJoinNode should output a completed scan group.");
            AssertEx.Equal("scan-A", scanGroup.ScanGroupId, "ScanGroupResult should keep the scan group id.");
            AssertEx.Equal(3, scanGroup.ActualFrameCount, "ScanGroupResult should include all preprocess results.");
            AssertEx.SequenceEqual(new[] { 0, 1, 2 }, scanGroup.Frames.Select(x => x.FrameIndex), "ScanGroupResult should be sorted by FrameIndex.");
            AssertEx.NotNull(final3D, "Final3D2DFusionNode should output Final3DImage.");
            AssertEx.NotNull(final2D, "Final3D2DFusionNode should output Final2DImage.");
            AssertEx.Equal("scan-A", Convert.ToString(final3D.Metadata["ScanGroupId"]), "Final3DImage should carry ScanGroupId metadata.");
            AssertEx.Equal("scan-A", Convert.ToString(final2D.Metadata["ScanGroupId"]), "Final2DImage should carry ScanGroupId metadata.");
            AssertEx.Equal(3, Convert.ToInt32(final3D.Metadata["SourceFrameCount"]), "Final3DImage should record source frame count.");
            AssertEx.Equal(3, Convert.ToInt32(final2D.Metadata["SourceFrameCount"]), "Final2DImage should record source frame count.");
        }

        public static async Task ScanGroupJoinBindingsReplaceAndFusionBinding()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateScanFusionBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-bind", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-bind", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-bind", 1)).ConfigureAwait(false);

            var scanGroup = FindOutput(sink, "scanJoin1", "ScanGroupResult") as ScanGroupResult;
            var final3D = FindOutput(sink, "fusion1", "Final3DImage") as IVisionImage;

            AssertEx.NotNull(scanGroup, "ScanGroupJoinNode should complete through PreprocessResultBinding.");
            AssertEx.SequenceEqual(new[] { 0, 1 }, scanGroup.Frames.Select(x => x.FrameIndex), "ScanGroupJoinNode should keep continuous FrameIndexes from settings.");
            AssertEx.NotNull(final3D, "Final3D2DFusionNode should resolve ScanGroupResultBinding.");
            AssertEx.Equal("scan-bind", Convert.ToString(final3D.Metadata["ScanGroupId"]), "Fusion output should carry the bound scan group id.");
        }

        public static async Task ScanGroupJoinDetectsNonContinuousFrameIndex()
        {
            var sink = new InMemoryFlowEventSink();
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            var runner = new FlowEngine(registry, sink).CreateRunner(CreateScanFusionBindingFlow());

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-gap", 0)).ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", CreateScanToken("scan-gap", 2)).ConfigureAwait(false);

            var failure = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.NodeFailed &&
                string.Equals(x.NodeId, "scanJoin1", StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(failure, "Non-continuous FrameIndex values should fail ScanGroupJoinNode when validation is enabled.");
            AssertEx.True(
                failure.Message.IndexOf("continuous", StringComparison.OrdinalIgnoreCase) >= 0,
                "ScanGroupJoinNode failure should identify continuous FrameIndex validation.");
        }

        private static RuntimeFlowDefinition CreateFrameGroupStitchFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-frame-group",
                FlowName = "Stage 08 Frame Group",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = FrameGroupJoinNodeFactory.TypeName,
                Name = "Frame Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "ExpectedShotCount", 2 },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "stitch1",
                Type = StitchNodeFactory.TypeName,
                Name = "Stitch",
                Version = "1.0.0",
                InputBindings =
                {
                    { "FrameGroup", VariableBinding.ForVariable("join1", "FrameGroup") }
                }
            });

            flow.Edges.Add(CreateEdge("join1", "Next", "stitch1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateDuplicateFrameGroupFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-frame-group-duplicate",
                FlowName = "Stage 08 Frame Group Duplicate",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = FrameGroupJoinNodeFactory.TypeName,
                Name = "Frame Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "ExpectedShotCount", 3 },
                    { "TimeoutMs", 1000 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateFrameGroupBindingFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-frame-group-binding",
                FlowName = "Stage 08 Frame Group Binding",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "join1",
                Type = FrameGroupJoinNodeFactory.TypeName,
                Name = "Frame Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "CaptureGroupIdBinding", "{{ token.CaptureGroupId }}" },
                    { "ShotIndexBinding", "{{ token.ShotIndex }}" },
                    { "FrameBinding", "{{ token.Frame }}" },
                    { "ExpectedShotCount", 2 },
                    { "DuplicatePolicy", "Replace" },
                    { "RequireContinuousShotIndex", true },
                    { "FirstShotIndex", 1 }
                }
            });

            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "join1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateScanFusionFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-scan-fusion",
                FlowName = "Stage 08 Scan Fusion",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "preprocess1",
                Type = FramePreprocessNodeFactory.TypeName,
                Name = "Frame Preprocess",
                Version = "1.0.0"
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "scanJoin1",
                Type = ScanGroupJoinNodeFactory.TypeName,
                Name = "Scan Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "ExpectedFrameCount", 3 },
                    { "TimeoutMs", 1000 }
                },
                InputBindings =
                {
                    { "PreprocessResult", VariableBinding.ForVariable("preprocess1", "PreprocessResult") }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "fusion1",
                Type = Final3D2DFusionNodeFactory.TypeName,
                Name = "Final Fusion",
                Version = "1.0.0",
                InputBindings =
                {
                    { "ScanGroupResult", VariableBinding.ForVariable("scanJoin1", "ScanGroupResult") }
                }
            });

            flow.Edges.Add(CreateEdge("preprocess1", "Next", "scanJoin1"));
            flow.Edges.Add(CreateEdge("scanJoin1", "Next", "fusion1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "preprocess1" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateScanFusionBindingFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "stage08-scan-fusion-binding",
                FlowName = "Stage 08 Scan Fusion Binding",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "preprocess1",
                Type = FramePreprocessNodeFactory.TypeName,
                Name = "Frame Preprocess",
                Version = "1.0.0",
                Settings =
                {
                    { "ScanGroupIdBinding", "{{ token.ScanGroupId }}" },
                    { "FrameIndexBinding", "{{ token.FrameIndex }}" },
                    { "ImageBinding", "{{ token.Image }}" },
                    { "FrameIdBinding", "{{ token.FrameId }}" }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "scanJoin1",
                Type = ScanGroupJoinNodeFactory.TypeName,
                Name = "Scan Group Join",
                Version = "1.0.0",
                Settings =
                {
                    { "PreprocessResultBinding", "{{ preprocess1.PreprocessResult }}" },
                    { "ExpectedFrameCount", 2 },
                    { "DuplicatePolicy", "Replace" },
                    { "RequireContinuousFrameIndex", true },
                    { "FirstFrameIndex", 0 }
                }
            });

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "fusion1",
                Type = Final3D2DFusionNodeFactory.TypeName,
                Name = "Final Fusion",
                Version = "1.0.0",
                Settings =
                {
                    { "ScanGroupResultBinding", "{{ scanJoin1.ScanGroupResult }}" }
                }
            });

            flow.Edges.Add(CreateEdge("preprocess1", "Next", "scanJoin1"));
            flow.Edges.Add(CreateEdge("scanJoin1", "Next", "fusion1"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "preprocess1" });
            return flow;
        }

        private static FlowToken CreateCaptureToken(string captureGroupId, int shotIndex)
        {
            var frame = CreateFrame(captureGroupId, shotIndex);
            var token = new FlowToken
            {
                TokenId = "token-" + captureGroupId + "-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                CaptureGroupId = captureGroupId,
                FrameId = frame.FrameId
            };
            token.Set("Frame", frame);
            token.Set("ShotIndex", shotIndex);
            return token;
        }

        private static FlowToken CreateScanToken(string scanGroupId, int frameIndex)
        {
            var image = new FakeVisionImage(
                "scan-" + scanGroupId + "-" + frameIndex.ToString(CultureInfo.InvariantCulture),
                320,
                120,
                "Mono8",
                null);
            var token = new FlowToken
            {
                TokenId = "token-" + scanGroupId + "-" + frameIndex.ToString(CultureInfo.InvariantCulture),
                ScanGroupId = scanGroupId,
                FrameId = image.ImageId
            };
            token.Set("Image", image);
            token.Set("FrameIndex", frameIndex);
            return token;
        }

        private static CameraFrameData CreateFrame(string captureGroupId, int shotIndex)
        {
            var image = new FakeVisionImage(
                "frame-" + captureGroupId + "-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                100 + shotIndex,
                80,
                "Mono8",
                null);
            var frame = new CameraFrameData
            {
                CameraId = "Camera01",
                TriggerId = "trigger-" + shotIndex.ToString(CultureInfo.InvariantCulture),
                FrameId = image.ImageId,
                GrabTime = DateTime.UtcNow,
                Image = image
            };
            frame.Metadata["CaptureGroupId"] = captureGroupId;
            frame.Metadata["ShotIndex"] = shotIndex;
            return frame;
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

        private static object FindOutput(InMemoryFlowEventSink sink, string nodeId, string outputName)
        {
            var variableName = nodeId + "." + outputName;
            var runtimeEvent = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data["VariableName"]), variableName, StringComparison.OrdinalIgnoreCase));

            AssertEx.NotNull(runtimeEvent, "Expected output was not produced: " + variableName);
            return runtimeEvent.Data["Value"];
        }
    }

    internal static class AdapterTests
    {
        public static Task RegistryGetsFakeCamera()
        {
            var registry = new DefaultDeviceRegistry();
            var camera = new FakeCameraAdapter("Camera01");

            registry.RegisterCamera(camera);

            ICameraAdapter resolvedByTryGet;
            AssertEx.True(registry.TryGetCamera("Camera01", out resolvedByTryGet), "Registry should find the registered fake camera.");
            AssertEx.True(object.ReferenceEquals(camera, resolvedByTryGet), "TryGetCamera should return the registered camera instance.");
            AssertEx.True(object.ReferenceEquals(camera, registry.GetCamera("Camera01")), "GetCamera should return the registered camera instance.");
            return Task.FromResult(0);
        }

        public static async Task SoftTriggerReceivesFrame()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var frameSource = new TaskCompletionSource<CameraFrameData>();

            camera.FrameArrived += delegate(object sender, CameraFrameArrivedEventArgs args)
            {
                frameSource.TrySetResult(args.Frame);
            };

            await camera.SoftTriggerAsync(
                new CameraTriggerContext
                {
                    CameraId = "Camera01",
                    TriggerId = "trigger-001"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.True(frameSource.Task.IsCompleted, "Default fake soft trigger should complete after FrameArrived is raised.");
            var completed = await Task.WhenAny(frameSource.Task, Task.Delay(1000)).ConfigureAwait(false);
            AssertEx.True(object.ReferenceEquals(frameSource.Task, completed), "Soft trigger should raise FrameArrived within the timeout.");

            var frame = await frameSource.Task.ConfigureAwait(false);
            AssertEx.NotNull(frame, "FrameArrived should provide frame data.");
            AssertEx.NotNull(frame.Image, "Frame data should include a fake image.");
            AssertEx.Equal("Camera01", frame.CameraId, "Frame camera id should match the adapter.");
            AssertEx.Equal("trigger-001", frame.TriggerId, "Frame trigger id should match the trigger context.");
            AssertEx.Equal("Camera01", Convert.ToString(frame.Metadata["CameraId"]), "Frame metadata should include CameraId.");
            AssertEx.Equal("trigger-001", Convert.ToString(frame.Metadata["TriggerId"]), "Frame metadata should include TriggerId.");
            AssertEx.True(frame.Metadata.ContainsKey("FrameId"), "Frame metadata should include FrameId.");
            AssertEx.True(frame.Metadata.ContainsKey("GrabTime"), "Frame metadata should include GrabTime.");
        }

        public static async Task SoftTriggerCancellationPreventsFrame()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 200
            };
            var frameRaised = false;
            camera.FrameArrived += delegate
            {
                frameRaised = true;
            };

            using (var cancellation = new CancellationTokenSource())
            {
                var triggerTask = camera.SoftTriggerAsync(
                    new CameraTriggerContext
                    {
                        CameraId = "Camera01",
                        TriggerId = "trigger-cancel"
                    },
                    cancellation.Token);
                cancellation.CancelAfter(10);

                await AssertEx.ThrowsAsync<OperationCanceledException>(
                    async delegate
                    {
                        await triggerTask.ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }

            await Task.Delay(250).ConfigureAwait(false);
            AssertEx.False(frameRaised, "Canceled fake soft trigger must not create a frame.");
        }

        public static async Task SoftTriggerCanReturnBeforeFrameArrived()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 50,
                ReturnBeforeFrameArrived = true
            };
            var frameSource = new TaskCompletionSource<CameraFrameData>();

            camera.FrameArrived += delegate(object sender, CameraFrameArrivedEventArgs args)
            {
                frameSource.TrySetResult(args.Frame);
            };

            await camera.SoftTriggerAsync(
                new CameraTriggerContext
                {
                    CameraId = "Camera01",
                    TriggerId = "trigger-background"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.False(frameSource.Task.IsCompleted, "ReturnBeforeFrameArrived should preserve async frame delivery behavior.");

            var completed = await Task.WhenAny(frameSource.Task, Task.Delay(1000)).ConfigureAwait(false);
            AssertEx.True(object.ReferenceEquals(frameSource.Task, completed), "Background fake frame should still arrive.");
            AssertEx.True(camera.LastError == null, "Background fake frame should not report adapter error.");

            var frame = await frameSource.Task.ConfigureAwait(false);
            AssertEx.Equal("trigger-background", frame.TriggerId, "Background fake frame should preserve trigger id.");
        }

        public static Task VisionImageReferenceLifecycle()
        {
            var native = new DisposableNativeImage();
            var image = new VisionImageReference("image-native", 10, 20, "Mono8", new byte[] { 1, 2, 3 }, native, true);
            image.Metadata["CameraId"] = "Camera01";

            byte[] bytes;
            AssertEx.True(image.TryGetBytes(out bytes), "VisionImageReference should expose bytes before disposal.");
            AssertEx.Equal(3, bytes.Length, "VisionImageReference should report byte length.");
            bytes[0] = 99;

            byte[] secondRead;
            AssertEx.True(image.TryGetBytes(out secondRead), "TryGetBytes should be repeatable.");
            AssertEx.Equal((byte)1, secondRead[0], "TryGetBytes should return a defensive copy.");

            var clone = image.CloneReference();
            AssertEx.False(object.ReferenceEquals(image, clone), "CloneReference should create a distinct image reference.");
            AssertEx.Equal("image-native", clone.ImageId, "CloneReference should preserve ImageId.");
            AssertEx.True(object.ReferenceEquals(image.NativeImage, clone.NativeImage), "CloneReference should preserve native image reference.");
            AssertEx.Equal("Camera01", Convert.ToString(clone.Metadata["CameraId"]), "CloneReference should copy metadata.");

            image.Dispose();

            AssertEx.True(image.IsDisposed, "Dispose should mark image disposed.");
            AssertEx.True(native.IsDisposed, "Dispose should release owned native image.");
            AssertEx.False(image.TryGetBytes(out bytes), "Disposed image should not expose bytes.");
            AssertEx.False(clone.IsDisposed, "CloneReference should not be disposed with the source image.");
            AssertEx.True(clone.TryGetBytes(out bytes), "CloneReference should still expose bytes after source disposal.");
            AssertEx.Equal((byte)1, bytes[0], "CloneReference should keep the referenced bytes.");
            clone.Dispose();
            return Task.FromResult(0);
        }

        public static Task FakeVisionImageLifecycle()
        {
            var native = new DisposableNativeImage();
            var image = new FakeVisionImage("fake-native", 5, 6, "RGB24", new byte[] { 7, 8 }, native, true);
            image.Metadata["FrameId"] = "frame-001";

            var clone = image.CloneReference();
            AssertEx.False(object.ReferenceEquals(image, clone), "FakeVisionImage CloneReference should create a distinct reference.");
            AssertEx.True(object.ReferenceEquals(image.NativeImage, clone.NativeImage), "FakeVisionImage clone should preserve native image reference.");
            AssertEx.Equal("frame-001", Convert.ToString(clone.Metadata["FrameId"]), "FakeVisionImage clone should copy metadata.");

            image.Dispose();

            byte[] bytes;
            AssertEx.True(native.IsDisposed, "FakeVisionImage should dispose owned native image.");
            AssertEx.False(image.TryGetBytes(out bytes), "Disposed FakeVisionImage should not expose bytes.");
            AssertEx.True(clone.TryGetBytes(out bytes), "FakeVisionImage clone should keep bytes after source disposal.");
            AssertEx.Equal(2, bytes.Length, "FakeVisionImage clone should keep byte length.");
            clone.Dispose();
            return Task.FromResult(0);
        }

        public static async Task FakeRecipeReturnsOk()
        {
            var recipe = new FakeRecipeAdapter("Recipe01");
            var result = await recipe.RunAsync(
                new RecipeRunRequest
                {
                    RecipeId = "Recipe01"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.NotNull(result, "Fake recipe should return a result.");
            AssertEx.True(result.IsSuccess, "Fake recipe should succeed.");
            AssertEx.Equal("OK", result.Status, "Fake recipe status should be OK.");
            AssertEx.Equal("Recipe01", Convert.ToString(result.Outputs["RecipeId"]), "Fake recipe output should include RecipeId.");
        }

        public static async Task FakeImageSaveReturnsPath()
        {
            var saver = new FakeImageSaveAdapter("ImageSave01");
            var result = await saver.SaveAsync(
                new ImageSaveRequest
                {
                    Image = new FakeVisionImage("image-001", 320, 240, "Mono8", null),
                    FileName = "part-a",
                    Format = "bmp"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.NotNull(result, "Fake image saver should return a result.");
            AssertEx.True(result.IsSuccess, "Fake image saver should succeed.");
            AssertEx.True(result.Path.IndexOf("fake://images", StringComparison.OrdinalIgnoreCase) == 0, "Fake image saver should use the fake base path.");
            AssertEx.True(result.Path.EndsWith("/part-a.bmp", StringComparison.OrdinalIgnoreCase), "Fake image saver should return a simulated file path.");
        }

        public static async Task FakeImageSaveSnapshotsImageReference()
        {
            var saver = new FakeImageSaveAdapter("ImageSave01");
            var image = new FakeVisionImage("image-snapshot", 320, 240, "Mono8", new byte[] { 1, 2, 3, 4 });

            var result = await saver.SaveAsync(
                new ImageSaveRequest
                {
                    Image = image,
                    FileName = "part-b",
                    Format = "png"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.Equal(4, Convert.ToInt32(result.Metadata["ByteLength"], CultureInfo.InvariantCulture), "Fake saver should record byte length.");
            AssertEx.Equal(false, Convert.ToBoolean(result.Metadata["HasNativeImage"], CultureInfo.InvariantCulture), "Fake saver should record native image state.");

            var savedRequests = saver.SnapshotSavedRequests();
            AssertEx.Equal(1, savedRequests.Count, "Fake saver should snapshot one request.");
            AssertEx.False(object.ReferenceEquals(image, savedRequests[0].Image), "Fake saver snapshot should clone the image reference.");
            image.Dispose();

            byte[] bytes;
            AssertEx.False(image.TryGetBytes(out bytes), "Source image should be disposed.");
            AssertEx.False(savedRequests[0].Image.IsDisposed, "Snapshot image reference should remain usable after source disposal.");
            AssertEx.True(savedRequests[0].Image.TryGetBytes(out bytes), "Snapshot image reference should expose bytes.");
            AssertEx.Equal(4, bytes.Length, "Snapshot image reference should keep bytes.");
        }

        private sealed class DisposableNativeImage : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }

    internal static class FlowTokenTests
    {
        public static Task SetGetTryGet()
        {
            var token = new FlowToken
            {
                ProductId = "P-001",
                WorkpieceId = "W-001"
            };

            token.Set("Score", 98);
            token.Set("Name", "part-a");
            token.Metadata["Line"] = "L1";

            AssertEx.Equal("P-001", token.ProductId, "ProductId should be stored.");
            AssertEx.Equal(98, token.Get<int>("Score"), "Integer value should round-trip.");
            AssertEx.Equal("part-a", token.Get<string>("Name"), "String value should round-trip.");
            AssertEx.Equal("L1", Convert.ToString(token.Metadata["Line"]), "Metadata value should round-trip.");

            int score;
            AssertEx.True(token.TryGet<int>("Score", out score), "TryGet should find Score.");
            AssertEx.Equal(98, score, "TryGet should return the converted Score.");

            object missing;
            AssertEx.False(token.TryGet("Missing", out missing), "TryGet should return false for missing keys.");
            return Task.FromResult(0);
        }
    }

    internal static class SerializationTests
    {
        public static Task RuntimeRoundTrip()
        {
            var runtime = CreateSampleRuntime();
            var json = RuntimeFlowSerializer.Serialize(runtime);
            var restored = RuntimeFlowSerializer.Deserialize(json);

            AssertEx.Equal("Station01_Main", restored.FlowId, "Runtime FlowId should round-trip.");
            AssertEx.Equal(2, restored.Nodes.Count, "Runtime nodes should round-trip.");
            AssertEx.Equal("camera.soft_trigger", restored.Nodes[0].Type, "Node type should round-trip.");
            AssertEx.Equal("Camera01", Convert.ToString(restored.Nodes[0].Settings["CameraId"]), "Node settings should round-trip.");
            AssertEx.Equal("camera_trigger_1.Image", restored.Nodes[1].InputBindings["Image"].GetVariableName(), "Input binding should round-trip.");
            AssertEx.Equal(1, restored.Edges.Count, "Runtime edges should round-trip.");
            AssertEx.Equal("ManualStart", restored.Entries[0].EntryName, "Runtime entry should round-trip.");
            AssertEx.False(json.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view zoom.");
            AssertEx.False(json.IndexOf("OffsetX", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain view offsets.");
            AssertEx.False(json.IndexOf("NodeViewState", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime JSON must not contain designer view types.");
            return Task.FromResult(0);
        }

        public static Task DesignRoundTrip()
        {
            var document = new FlowDesignDocument
            {
                FlowId = "Station01_Main",
                FlowName = "Station01 Main Flow",
                Runtime = CreateSampleRuntime(),
                View = new FlowViewState
                {
                    Zoom = 1.25,
                    OffsetX = 12,
                    OffsetY = 34
                }
            };
            document.View.Nodes["camera_trigger_1"] = new NodeViewState
            {
                X = 100,
                Y = 200,
                IsCollapsed = true
            };

            var json = FlowDesignSerializer.Serialize(document);
            var restored = FlowDesignSerializer.Deserialize(json);

            AssertEx.Equal("Station01_Main", restored.FlowId, "Design FlowId should round-trip.");
            AssertEx.Equal("Station01_Main", restored.Runtime.FlowId, "Design runtime should round-trip.");
            AssertEx.Equal(1.25, restored.View.Zoom, "View zoom should round-trip.");
            AssertEx.Equal(100.0, restored.View.Nodes["camera_trigger_1"].X, "Node X should round-trip.");
            AssertEx.True(restored.View.Nodes["camera_trigger_1"].IsCollapsed, "Node collapsed state should round-trip.");
            return Task.FromResult(0);
        }

        private static RuntimeFlowDefinition CreateSampleRuntime()
        {
            var runtime = new RuntimeFlowDefinition
            {
                FlowId = "Station01_Main",
                FlowName = "Station01 Main Flow",
                Version = "1.0.0"
            };

            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "camera_trigger_1",
                Type = "camera.soft_trigger",
                Name = "Camera Soft Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { "CameraId", "Camera01" },
                    { "TimeoutMs", 1000 }
                }
            });

            runtime.Nodes.Add(new NodeDefinition
            {
                Id = "image_save_1",
                Type = "image.save",
                Name = "Save Image",
                Version = "1.0.0",
                InputBindings =
                {
                    { "Image", VariableBinding.ForVariable("camera_trigger_1", "Image") }
                }
            });

            runtime.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "camera_trigger_1",
                FromPort = "Next",
                ToNodeId = "image_save_1",
                ToPort = "In"
            });

            runtime.Entries.Add(new FlowEntryDefinition
            {
                EntryName = "ManualStart",
                TargetNodeId = "camera_trigger_1"
            });

            return runtime;
        }
    }

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

            AssertHasIssue(result, "NodeIdDuplicate", "Duplicate NodeId should be reported.");
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

            AssertHasIssue(result, "EdgeTargetMissing", "Dangling edge target should be reported.");
            return Task.FromResult(0);
        }

        public static Task MissingRequiredSettingReturnsError()
        {
            var flow = CreateValidRuntime();
            flow.Nodes[0].Settings.Clear();

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, "RequiredSettingMissing", "Missing required DelayMs should be reported.");
            return Task.FromResult(0);
        }

        public static Task MissingBindingOutputReturnsError()
        {
            var flow = CreateValidRuntime();
            flow.Nodes[1].InputBindings["Message"] = VariableBinding.ForVariable("delay1", "MissingOutput");

            var result = CreateValidator().Validate(flow);

            AssertHasIssue(result, "BindingOutputMissing", "Missing source output should be reported.");
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

            AssertHasIssue(result, "SettingValueInvalid", "Invalid StreamFrames numeric settings should be reported.");
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
                    { "QueueFullMode", "Drop" }
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

            AssertHasIssue(result, "QueueFullModeInvalid", "Invalid QueueFullMode should be reported.");
            AssertHasIssue(result, "DuplicatePolicyInvalid", "Invalid DuplicatePolicy should be reported.");
            AssertEx.True(
                result.Issues.Count(x => string.Equals(x.Code, "SettingValueInvalid", StringComparison.OrdinalIgnoreCase)) >= 4,
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

    internal static class SampleFlowTests
    {
        public static Task SampleFlowFilesDeserializeAndValidate()
        {
            var sampleDirectory = GetSampleDirectory();
            var registry = CreateRegistry();
            var validator = new FlowValidator(registry);
            var publisher = new FlowPublishService(registry);

            ValidateRuntimeFile(Path.Combine(sampleDirectory, "single-shot.flowruntime"), validator);

            var designFiles = new[]
            {
                "single-shot.flowdesign",
                "two-position-stitch.flowdesign",
                "continuous-scan.flowdesign"
            };

            for (var index = 0; index < designFiles.Length; index++)
            {
                var path = Path.Combine(sampleDirectory, designFiles[index]);
                AssertEx.True(File.Exists(path), "Sample design should exist: " + path);

                var document = FlowDesignSerializer.Load(path);
                AssertEx.NotNull(document, "Sample design should deserialize: " + path);
                AssertEx.NotNull(document.Runtime, "Sample design should include runtime: " + path);
                AssertEx.NotNull(document.View, "Sample design should include view state: " + path);
                AssertEx.True(document.View.Nodes.Count > 0, "Sample design should include node coordinates: " + path);

                var publishResult = publisher.Publish(document);
                AssertValid(publishResult.Validation, path);
                AssertEx.NotNull(publishResult.Runtime, "Published sample runtime should be available: " + path);

                var runtimeJson = RuntimeFlowSerializer.Serialize(publishResult.Runtime);
                AssertNoViewState(runtimeJson, path);
            }

            return Task.FromResult(0);
        }

        public static Task ContinuousScanPublishesRuntimeWithEnhancedRules()
        {
            var sampleDirectory = GetSampleDirectory();
            var path = Path.Combine(sampleDirectory, "continuous-scan.flowdesign");
            var document = FlowDesignSerializer.Load(path);
            var result = new FlowPublishService(CreateRegistry()).Publish(document);

            AssertValid(result.Validation, path);
            AssertEx.NotNull(result.Runtime, "Published continuous scan runtime should be available.");

            var scanJoin = result.Runtime.Nodes.FirstOrDefault(x => string.Equals(x.Id, "scan_join_1", StringComparison.OrdinalIgnoreCase));
            AssertEx.NotNull(scanJoin, "Continuous scan sample should contain scan_join_1.");
            AssertEx.Equal(true, Convert.ToBoolean(scanJoin.Settings["RequireContinuousFrameIndex"], CultureInfo.InvariantCulture), "Sample scan join should require continuous frame indexes.");
            AssertEx.Equal("Replace", Convert.ToString(scanJoin.Settings["DuplicatePolicy"], CultureInfo.InvariantCulture), "Sample scan join should use Replace duplicate policy.");

            var runtimeJson = RuntimeFlowSerializer.Serialize(result.Runtime);
            AssertNoViewState(runtimeJson, path);
            AssertEx.False(runtimeJson.IndexOf("\"runtime\"", StringComparison.OrdinalIgnoreCase) >= 0, "Published runtime JSON should not contain a nested runtime document.");
            return Task.FromResult(0);
        }

        public static Task SampleRuntimeExcludesViewState()
        {
            var sampleDirectory = GetSampleDirectory();
            var runtimePath = Path.Combine(sampleDirectory, "single-shot.flowruntime");
            AssertEx.True(File.Exists(runtimePath), "Sample runtime should exist: " + runtimePath);

            var runtime = RuntimeFlowSerializer.Load(runtimePath);
            AssertEx.NotNull(runtime, "Sample runtime should deserialize.");
            AssertEx.Equal("single-shot", runtime.FlowId, "Sample runtime FlowId should match.");

            var runtimeJson = File.ReadAllText(runtimePath);
            AssertNoViewState(runtimeJson, runtimePath);
            return Task.FromResult(0);
        }

        private static void ValidateRuntimeFile(string path, FlowValidator validator)
        {
            AssertEx.True(File.Exists(path), "Sample runtime should exist: " + path);
            var runtime = RuntimeFlowSerializer.Load(path);
            AssertEx.NotNull(runtime, "Sample runtime should deserialize: " + path);
            AssertValid(validator.Validate(runtime), path);
        }

        private static NodeRegistry CreateRegistry()
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            return registry;
        }

        private static string GetSampleDirectory()
        {
            var roots = new[]
            {
                Environment.CurrentDirectory,
                AppDomain.CurrentDomain.BaseDirectory
            };

            for (var index = 0; index < roots.Length; index++)
            {
                var root = roots[index];
                for (var depth = 0; depth < 10 && !string.IsNullOrWhiteSpace(root); depth++)
                {
                    var candidate = Path.Combine(root, "samples", "flows");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }

                    var parent = Directory.GetParent(root);
                    root = parent == null ? null : parent.FullName;
                }
            }

            throw new InvalidOperationException("Could not locate samples/flows from current test directory.");
        }

        private static void AssertValid(FlowValidationResult result, string path)
        {
            AssertEx.NotNull(result, "Validation result should be available: " + path);
            if (!result.IsValid)
            {
                var issues = string.Join(
                    "; ",
                    result.Issues.Select(x =>
                        x.Severity + " " +
                        x.Code + " node=" +
                        x.NodeId + " edge=" +
                        x.EdgeIndex + " entry=" +
                        x.EntryName + " field=" +
                        x.Field + " " +
                        x.Message).ToArray());
                throw new InvalidOperationException("Sample flow should validate: " + path + ". Issues: " + issues);
            }
        }

        private static void AssertNoViewState(string json, string path)
        {
            AssertEx.False(json.IndexOf("\"view\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain view object: " + path);
            AssertEx.False(json.IndexOf("\"zoom\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain canvas zoom: " + path);
            AssertEx.False(json.IndexOf("\"offsetX\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain canvas offset: " + path);
            AssertEx.False(json.IndexOf("\"nodes\"", StringComparison.OrdinalIgnoreCase) >= 0 && json.IndexOf("\"x\"", StringComparison.OrdinalIgnoreCase) >= 0 && json.IndexOf("\"y\"", StringComparison.OrdinalIgnoreCase) >= 0 && json.IndexOf("\"runtime\"", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain designer node coordinates: " + path);
            AssertEx.False(json.IndexOf("NodeViewState", StringComparison.OrdinalIgnoreCase) >= 0, "Runtime must not contain designer view types: " + path);
        }
    }

    internal static class FlowRunnerTests
    {
        public static async Task LinearOrderAndVariables()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateLinearFlow(includeOutputs: true), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-linear" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "C" }, executionLog, "Nodes should execute in A -> B -> C order.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.OutputProduced && Convert.ToString(x.Data["VariableName"]) == "A.Value"), "A.Value output should be written.");
            AssertEx.True(sink.Events.Any(x => x.EventType == FlowRuntimeEventType.OutputProduced && Convert.ToString(x.Data["VariableName"]) == "B.Value"), "B.Value output should be written.");
        }

        public static async Task FanOutExecutesAllOutgoingEdges()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateFanOutFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-fanout" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "C" }, executionLog, "All edges from A.Next should execute in definition order.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeCompleted && string.Equals(x.NodeId, "B", StringComparison.OrdinalIgnoreCase)),
                "Fan-out branch B should complete.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.NodeCompleted && string.Equals(x.NodeId, "C", StringComparison.OrdinalIgnoreCase)),
                "Fan-out branch C should complete.");
        }

        public static async Task BranchedFanOutGraphExecutesAllBranches()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateBranchedFanOutFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-branched-fanout" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "D", "C", "E" }, executionLog, "Both fan-out branches should execute their downstream chains.");
        }

        public static async Task ReconvergingBranchesCanReachSameNode()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateReconvergingFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-reconverge" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "B", "D", "C", "D" }, executionLog, "Path-level cycle detection should not block a valid reconverging node.");
        }

        public static async Task NodeFailedAndErrorRoute()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateFailureFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-failure" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "ErrorHandler" }, executionLog, "Failure should follow the Error route.");
            var failedEvent = sink.Events.FirstOrDefault(x => x.EventType == FlowRuntimeEventType.NodeFailed);
            AssertEx.NotNull(failedEvent, "NodeFailed event should be published.");
            AssertEx.Equal("A", failedEvent.NodeId, "NodeFailed should identify the failing node.");
            AssertEx.Equal("Error", failedEvent.OutputPort, "NodeFailed should use the Error output port.");
        }

        public static async Task NodeTimeoutAndTimeoutRoute()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateTimeoutFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-timeout-route" }).ConfigureAwait(false);

            AssertEx.SequenceEqual(new[] { "A", "TimeoutHandler" }, executionLog, "Timeout should follow the Timeout route.");
            var timeoutEvent = sink.Events.FirstOrDefault(x => x.EventType == FlowRuntimeEventType.NodeTimeout);
            AssertEx.NotNull(timeoutEvent, "NodeTimeout event should be published.");
            AssertEx.Equal("A", timeoutEvent.NodeId, "NodeTimeout should identify the timed out node.");
            AssertEx.Equal("Timeout", timeoutEvent.OutputPort, "NodeTimeout should use the configured Timeout output port.");
        }

        public static async Task CycleRouteThrows()
        {
            var runner = CreateRunner(CreateCycleFlow(), new List<string>(), new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);

            var exception = await AssertEx.ThrowsAsync<InvalidOperationException>(
                () => runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-cycle" })).ConfigureAwait(false);

            AssertEx.True(exception.Message.IndexOf("Cycle detected", StringComparison.OrdinalIgnoreCase) >= 0, "Cycle detection should report a clear error.");
        }

        public static async Task MissingEntryThrows()
        {
            var runner = CreateRunner(CreateLinearFlow(includeOutputs: false), new List<string>(), new InMemoryFlowEventSink());
            await runner.StartAsync().ConfigureAwait(false);

            var exception = await AssertEx.ThrowsAsync<ArgumentException>(
                () => runner.TriggerAsync("MissingEntry", new FlowToken())).ConfigureAwait(false);

            AssertEx.True(exception.Message.IndexOf("MissingEntry", StringComparison.OrdinalIgnoreCase) >= 0, "Missing entry exception should include the entry name.");
        }

        public static async Task RuntimeEventOrder()
        {
            var executionLog = new List<string>();
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateSingleNodeFlow(), executionLog, sink);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-events" }).ConfigureAwait(false);

            var eventTypes = sink.Events.Select(x => x.EventType).ToList();
            AssertEx.SequenceEqual(
                new[]
                {
                    FlowRuntimeEventType.FlowStarted,
                    FlowRuntimeEventType.TokenCreated,
                    FlowRuntimeEventType.NodeStarted,
                    FlowRuntimeEventType.NodeCompleted
                },
                eventTypes,
                "Runtime events should be published in execution order.");
        }

        private static IFlowRunner CreateRunner(RuntimeFlowDefinition flow, IList<string> executionLog, InMemoryFlowEventSink sink)
        {
            var registry = new NodeRegistry();
            registry.Register(new RecordingNodeFactory(executionLog));
            return new FlowEngine(registry, sink).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateLinearFlow(bool includeOutputs)
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "linear",
                FlowName = "Linear",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", includeOutputs ? "Value" : null, null));
            flow.Nodes.Add(CreateNode("B", includeOutputs ? "Value" : null, includeOutputs ? "A.Value" : null));
            flow.Nodes.Add(CreateNode("C", null, includeOutputs ? "B.Value" : null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("B", "Next", "C"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateFanOutFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "fanout",
                FlowName = "Fan Out",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Nodes.Add(CreateNode("C", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("A", "Next", "C"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateBranchedFanOutFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "branched-fanout",
                FlowName = "Branched Fan Out",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Nodes.Add(CreateNode("C", null, null));
            flow.Nodes.Add(CreateNode("D", null, null));
            flow.Nodes.Add(CreateNode("E", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("A", "Next", "C"));
            flow.Edges.Add(CreateEdge("B", "Next", "D"));
            flow.Edges.Add(CreateEdge("C", "Next", "E"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateReconvergingFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "reconverging",
                FlowName = "Reconverging",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Nodes.Add(CreateNode("C", null, null));
            flow.Nodes.Add(CreateNode("D", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("A", "Next", "C"));
            flow.Edges.Add(CreateEdge("B", "Next", "D"));
            flow.Edges.Add(CreateEdge("C", "Next", "D"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateFailureFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "failure",
                FlowName = "Failure",
                Version = "1.0.0"
            };

            var failing = CreateNode("A", null, null);
            failing.Settings["Mode"] = "Fail";
            flow.Nodes.Add(failing);
            flow.Nodes.Add(CreateNode("ErrorHandler", null, null));
            flow.Edges.Add(CreateEdge("A", "Error", "ErrorHandler"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateTimeoutFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "timeout",
                FlowName = "Timeout",
                Version = "1.0.0"
            };

            var timedOut = CreateNode("A", null, null);
            timedOut.Settings["Mode"] = "Timeout";
            timedOut.Settings["TimeoutOutputPort"] = "Timeout";
            flow.Nodes.Add(timedOut);
            flow.Nodes.Add(CreateNode("TimeoutHandler", null, null));
            flow.Edges.Add(CreateEdge("A", "Timeout", "TimeoutHandler"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateCycleFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "cycle",
                FlowName = "Cycle",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Nodes.Add(CreateNode("B", null, null));
            flow.Edges.Add(CreateEdge("A", "Next", "B"));
            flow.Edges.Add(CreateEdge("B", "Next", "A"));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static RuntimeFlowDefinition CreateSingleNodeFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "single",
                FlowName = "Single",
                Version = "1.0.0"
            };

            flow.Nodes.Add(CreateNode("A", null, null));
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "A" });
            return flow;
        }

        private static NodeDefinition CreateNode(string id, string outputName, string requiredVariable)
        {
            var node = new NodeDefinition
            {
                Id = id,
                Type = RecordingNodeFactory.TypeName,
                Name = id,
                Version = "1.0.0"
            };

            if (!string.IsNullOrWhiteSpace(outputName))
            {
                node.Settings["OutputName"] = outputName;
                node.Settings["OutputValue"] = id + "-output";
            }

            if (!string.IsNullOrWhiteSpace(requiredVariable))
            {
                node.Settings["RequiredVariable"] = requiredVariable;
            }

            return node;
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

    internal static class FlowTaskQueueTests
    {
        public static async Task CapacityRejectsAndPublishesEvents()
        {
            var sink = new InMemoryFlowEventSink();
            var queue = new FlowTaskQueue(
                new FlowTaskQueueOptions
                {
                    QueueName = "save",
                    Capacity = 1,
                    MaxDegreeOfParallelism = 1,
                    FullMode = FlowTaskQueueFullMode.Reject
                },
                sink);
            var release = new TaskCompletionSource<int>();
            var firstStarted = new TaskCompletionSource<int>();
            var context = new FlowTaskQueueItemContext
            {
                FlowId = "flow-queue",
                TokenId = "token-queue",
                NodeId = "save1",
                OperationName = "SaveImage"
            };

            var first = queue.EnqueueAsync<int>(
                async delegate(CancellationToken token)
                {
                    firstStarted.TrySetResult(0);
                    await release.Task.ConfigureAwait(false);
                    return 7;
                },
                context,
                CancellationToken.None);

            await firstStarted.Task.ConfigureAwait(false);
            AssertEx.Equal(1, queue.CurrentDepth, "Queue depth should count the running first item.");

            var rejected = await queue.EnqueueAsync<int>(
                delegate(CancellationToken token)
                {
                    return Task.FromResult(9);
                },
                context,
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.True(rejected.IsRejected, "Queue should reject when capacity is full and FullMode is Reject.");
            AssertEx.False(rejected.IsAccepted, "Rejected queue work should not be accepted.");
            release.TrySetResult(0);

            var firstResult = await first.ConfigureAwait(false);
            AssertEx.True(firstResult.IsSuccess, "First queue work should complete successfully.");
            AssertEx.Equal(7, firstResult.Value, "Queue should return work result.");
            AssertEx.Equal(0, queue.CurrentDepth, "Queue depth should return to zero after completion.");

            var events = sink.Events.Select(x => x.EventType).ToList();
            AssertEx.True(events.Contains(FlowRuntimeEventType.QueueEnqueued), "QueueEnqueued event should be published.");
            AssertEx.True(events.Contains(FlowRuntimeEventType.QueueStarted), "QueueStarted event should be published.");
            AssertEx.True(events.Contains(FlowRuntimeEventType.QueueRejected), "QueueRejected event should be published.");
            AssertEx.True(events.Contains(FlowRuntimeEventType.QueueCompleted), "QueueCompleted event should be published.");
            AssertEx.True(
                sink.Events.Any(x => string.Equals(Convert.ToString(x.Data["QueueName"]), "save", StringComparison.OrdinalIgnoreCase)),
                "Queue events should include queue name.");
        }

        public static Task RegistryReusesNamedQueues()
        {
            var registry = new FlowTaskQueueRegistry(new InMemoryFlowEventSink());
            var first = registry.GetOrCreate(
                "save",
                new FlowTaskQueueOptions
                {
                    QueueName = "save",
                    Capacity = 2,
                    MaxDegreeOfParallelism = 1
                });
            var second = registry.GetOrCreate("save");

            FlowTaskQueue resolved;
            AssertEx.True(object.ReferenceEquals(first, second), "Queue registry should reuse named queue instances.");
            AssertEx.True(registry.TryGetQueue("save", out resolved), "Queue registry should resolve existing queue.");
            AssertEx.True(object.ReferenceEquals(first, resolved), "TryGetQueue should return the registered queue.");
            AssertEx.Equal(2, first.Capacity, "Queue registry should preserve initial options.");
            return Task.FromResult(0);
        }
    }

    internal sealed class RecordingNodeFactory : INodeFactory
    {
        public const string TypeName = "test.record";
        private readonly IList<string> _executionLog;

        public RecordingNodeFactory(IList<string> executionLog)
        {
            _executionLog = executionLog;
        }

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
                    DisplayName = "Recording Test Node",
                    Version = "1.0.0"
                };
            }
        }

        public IFlowNode Create(NodeDefinition definition)
        {
            return new RecordingNode(definition, _executionLog);
        }
    }

    internal sealed class RecordingNode : IFlowNode
    {
        private readonly NodeDefinition _definition;
        private readonly IList<string> _executionLog;

        public RecordingNode(NodeDefinition definition, IList<string> executionLog)
        {
            _definition = definition;
            _executionLog = executionLog;
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _executionLog.Add(_definition.Id);

            var mode = GetSetting("Mode");
            if (string.Equals(mode, "Fail", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(NodeExecutionResult.Failure("Requested failure."));
            }

            if (string.Equals(mode, "Timeout", StringComparison.OrdinalIgnoreCase))
            {
                var timeoutOutputPort = GetSetting("TimeoutOutputPort");
                return Task.FromResult(NodeExecutionResult.Timeout("Requested timeout.", timeoutOutputPort));
            }

            var requiredVariable = GetSetting("RequiredVariable");
            if (!string.IsNullOrWhiteSpace(requiredVariable))
            {
                object value;
                if (!context.Variables.TryGet(requiredVariable, out value))
                {
                    return Task.FromResult(NodeExecutionResult.Failure("Required variable was missing: " + requiredVariable));
                }
            }

            var outputs = new Dictionary<string, object>();
            var outputName = GetSetting("OutputName");
            if (!string.IsNullOrWhiteSpace(outputName))
            {
                outputs[outputName] = GetSetting("OutputValue");
            }

            return Task.FromResult(NodeExecutionResult.Success("Next", outputs));
        }

        private string GetSetting(string name)
        {
            object value;
            if (_definition.Settings != null && _definition.Settings.TryGetValue(name, out value))
            {
                return Convert.ToString(value);
            }

            return null;
        }
    }

    internal static class AssertEx
    {
        public static void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        public static void False(bool condition, string message)
        {
            True(!condition, message);
        }

        public static void NotNull(object value, string message)
        {
            True(value != null, message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + ", Actual: " + actual);
            }
        }

        public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
        {
            var expectedList = expected.ToList();
            var actualList = actual.ToList();
            if (expectedList.Count != actualList.Count)
            {
                throw new InvalidOperationException(message + " Expected count: " + expectedList.Count + ", Actual count: " + actualList.Count + ". Actual: " + string.Join(", ", actualList));
            }

            for (var index = 0; index < expectedList.Count; index++)
            {
                if (!object.Equals(expectedList[index], actualList[index]))
                {
                    throw new InvalidOperationException(message + " Difference at index " + index + ". Expected: " + expectedList[index] + ", Actual: " + actualList[index]);
                }
            }
        }

        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException ex)
            {
                return ex;
            }

            throw new InvalidOperationException("Expected exception was not thrown: " + typeof(TException).FullName);
        }
    }
}
