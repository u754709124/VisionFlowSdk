using System;
using System.Collections.Generic;
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
                new TestCase("FlowRunner executes A -> B -> C and writes output variables", FlowRunnerTests.LinearOrderAndVariables),
                new TestCase("FlowRunner publishes NodeFailed and follows Error route", FlowRunnerTests.NodeFailedAndErrorRoute),
                new TestCase("FlowRunner reports a clear missing entry exception", FlowRunnerTests.MissingEntryThrows),
                new TestCase("FlowRunner publishes runtime events in order", FlowRunnerTests.RuntimeEventOrder),
                new TestCase("DefaultDeviceRegistry resolves a fake camera", AdapterTests.RegistryGetsFakeCamera),
                new TestCase("FakeCameraAdapter soft trigger raises FrameArrived", AdapterTests.SoftTriggerReceivesFrame),
                new TestCase("FakeRecipeAdapter returns OK", AdapterTests.FakeRecipeReturnsOk),
                new TestCase("FakeImageSaveAdapter returns a simulated path", AdapterTests.FakeImageSaveReturnsPath),
                new TestCase("CommonNodeRegistration resolves common factories", CommonNodeTests.RegisterAllResolvesFactories),
                new TestCase("LogNode publishes a runtime log event", CommonNodeTests.LogNodePublishesRuntimeEvent),
                new TestCase("DelayNode executes a configured delay", CommonNodeTests.DelayNodeExecutes),
                new TestCase("VariableSetNode writes a variable subsequent node can read", CommonNodeTests.VariableSetNodeWritesVariableForNextNode)
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
                return Task.FromResult(NodeExecutionResult.Timeout("Requested timeout."));
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
