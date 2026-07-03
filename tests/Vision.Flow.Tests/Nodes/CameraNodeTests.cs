using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    internal static class CameraNodeTests
    {
        public static async Task SoftTriggerWaitsForOneFrame()
        {
            var camera = new TestCameraAdapter("Camera01");
            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateSingleCameraNodeFlow("camera1", CameraSoftTriggerNodeFactory.TypeName, delegate(NodeDefinition node)
            {
                node.Settings[FlowSettingNames.CameraId] = "Camera01";
                node.Settings[FlowSettingNames.TimeoutMs] = 1000;
            }), sink, new TestDeviceRegistry(camera), null);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-soft" }).ConfigureAwait(false);

            AssertEx.Equal(1, camera.GrabOneCallCount, "Soft trigger node should call GrabOneAsync once.");
            var imageOutput = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName], CultureInfo.InvariantCulture), "camera1.Image", StringComparison.OrdinalIgnoreCase));
            AssertEx.NotNull(imageOutput, "Soft trigger node should output an image.");

            var frameOutput = sink.Events.FirstOrDefault(x =>
                x.EventType == FlowRuntimeEventType.OutputProduced &&
                string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.VariableName], CultureInfo.InvariantCulture), "camera1.Frame", StringComparison.OrdinalIgnoreCase));
            AssertEx.NotNull(frameOutput, "Soft trigger node should output frame data.");
            AssertEx.True(
                sink.Events.Any(x => x.EventType == FlowRuntimeEventType.ImageProduced && string.Equals(x.NodeId, "camera1", StringComparison.OrdinalIgnoreCase)),
                "Soft trigger node should publish ImageProduced.");
        }

        public static async Task HardTriggerDispatchesOffCallbackThread()
        {
            var camera = new TestCameraAdapter("Camera01");
            var sink = new InMemoryFlowEventSink();
            var executions = new List<ThreadProbeExecution>();
            var runner = CreateRunner(CreateHardTriggerFlow(), sink, new TestDeviceRegistry(camera), executions);

            await runner.StartAsync().ConfigureAwait(false);
            var callbackThreadId = camera.EmitFrameOnDedicatedThread("hard-trigger", "hard-frame");
            await WaitForExecutionAsync(executions, 1).ConfigureAwait(false);
            await runner.StopAsync().ConfigureAwait(false);

            var execution = executions[0];
            AssertEx.Equal("probe1", execution.NodeId, "Hard trigger should dispatch the downstream node.");
            AssertEx.False(callbackThreadId == execution.ThreadId, "Downstream node should not execute on the camera callback thread.");
            AssertEx.True(execution.HasFrameVariable, "Downstream node should receive the hard trigger frame variable.");
        }

        public static async Task ParameterNodeSetsWritableParameter()
        {
            var camera = new TestCameraAdapter("Camera01");
            camera.Parameters.Add(new CameraParameterDescriptor
            {
                ParameterName = "ExposureTime",
                DisplayName = "Exposure Time",
                ValueType = "Double",
                IsWritable = true
            });

            var sink = new InMemoryFlowEventSink();
            var runner = CreateRunner(CreateSingleCameraNodeFlow("param1", CameraParameterSetNodeFactory.TypeName, delegate(NodeDefinition node)
            {
                node.Settings[FlowSettingNames.CameraId] = "Camera01";
                node.Settings[FlowSettingNames.ParameterName] = "ExposureTime";
                node.Settings[FlowSettingNames.Value] = "12.5";
            }), sink, new TestDeviceRegistry(camera), null);

            await runner.StartAsync().ConfigureAwait(false);
            await runner.TriggerAsync("ManualStart", new FlowToken { TokenId = "token-param" }).ConfigureAwait(false);

            object value;
            AssertEx.True(camera.SetValues.TryGetValue("ExposureTime", out value), "Parameter node should call SetParameterAsync.");
            AssertEx.Equal(12.5, value, "Parameter node should convert the value through the descriptor type.");
        }

        private static IFlowRunner CreateRunner(RuntimeFlowDefinition flow, InMemoryFlowEventSink sink, IDeviceRegistry devices, IList<ThreadProbeExecution> executions)
        {
            var registry = new NodeRegistry();
            CommonNodeRegistration.RegisterAll(registry);
            if (executions != null)
            {
                registry.Register(new ThreadProbeNodeFactory(executions));
            }

            return new FlowEngine(registry, sink, devices).CreateRunner(flow);
        }

        private static RuntimeFlowDefinition CreateSingleCameraNodeFlow(string nodeId, string nodeType, Action<NodeDefinition> configure)
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
            configure(node);
            flow.Nodes.Add(node);
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = nodeId });
            return flow;
        }

        private static RuntimeFlowDefinition CreateHardTriggerFlow()
        {
            var flow = new RuntimeFlowDefinition
            {
                FlowId = "hard-trigger-flow",
                FlowName = "Hard Trigger Flow",
                Version = "1.0.0"
            };

            flow.Nodes.Add(new NodeDefinition
            {
                Id = "hard1",
                Type = CameraHardTriggerNodeFactory.TypeName,
                Name = "Hard Trigger",
                Version = "1.0.0",
                Settings =
                {
                    { FlowSettingNames.CameraId, "Camera01" }
                }
            });
            flow.Nodes.Add(new NodeDefinition
            {
                Id = "probe1",
                Type = ThreadProbeNodeFactory.TypeName,
                Name = "Thread Probe",
                Version = "1.0.0"
            });
            flow.Edges.Add(new EdgeDefinition
            {
                FromNodeId = "hard1",
                FromPort = FlowPortNames.Frame,
                ToNodeId = "probe1",
                ToPort = FlowPortNames.In
            });
            flow.Entries.Add(new FlowEntryDefinition { EntryName = "ManualStart", TargetNodeId = "hard1" });
            return flow;
        }

        private static async Task WaitForExecutionAsync(ICollection<ThreadProbeExecution> executions, int expectedCount)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                lock (executions)
                {
                    if (executions.Count >= expectedCount)
                    {
                        return;
                    }
                }

                await Task.Delay(10).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for hard trigger dispatch.");
        }

        private sealed class TestDeviceRegistry : IDeviceRegistry
        {
            private readonly ICameraAdapter _camera;

            public TestDeviceRegistry(ICameraAdapter camera)
            {
                _camera = camera;
            }

            public bool TryGetCamera(string cameraId, out ICameraAdapter camera)
            {
                camera = string.Equals(_camera.CameraId, cameraId, StringComparison.OrdinalIgnoreCase) ? _camera : null;
                return camera != null;
            }

            public ICameraAdapter GetCamera(string cameraId)
            {
                ICameraAdapter camera;
                if (!TryGetCamera(cameraId, out camera))
                {
                    throw new InvalidOperationException("Camera not found: " + cameraId);
                }

                return camera;
            }
        }

        private sealed class TestCameraAdapter : ICameraAdapter
        {
            public TestCameraAdapter(string cameraId)
            {
                CameraId = cameraId;
                Parameters = new List<CameraParameterDescriptor>();
                SetValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }

            public event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;

            public string CameraId { get; private set; }

            public List<CameraParameterDescriptor> Parameters { get; private set; }

            public Dictionary<string, object> SetValues { get; private set; }

            public int GrabOneCallCount { get; private set; }

            public IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors()
            {
                return Parameters;
            }

            public Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetValues[parameterName] = value;
                return Task.FromResult(0);
            }

            public Task<object> GetParameterAsync(string parameterName, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object value;
                SetValues.TryGetValue(parameterName, out value);
                return Task.FromResult(value);
            }

            public Task<CameraFrameData> GrabOneAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                GrabOneCallCount++;
                return Task.FromResult(CreateFrame("grab-one", "soft-frame"));
            }

            public int EmitFrameOnDedicatedThread(string triggerId, string frameId)
            {
                var callbackThreadId = 0;
                var thread = new Thread(delegate()
                {
                    callbackThreadId = Thread.CurrentThread.ManagedThreadId;
                    EmitFrame(triggerId, frameId);
                });
                thread.IsBackground = true;
                thread.Start();
                thread.Join();
                return callbackThreadId;
            }

            private void EmitFrame(string triggerId, string frameId)
            {
                var handler = FrameArrived;
                if (handler == null)
                {
                    return;
                }

                handler(this, new CameraFrameArrivedEventArgs(CreateFrame(triggerId, frameId)));
            }

            private CameraFrameData CreateFrame(string triggerId, string frameId)
            {
                return new CameraFrameData
                {
                    CameraId = CameraId,
                    TriggerId = triggerId,
                    FrameId = frameId,
                    GrabTime = DateTime.UtcNow,
                    Image = new VisionImageReference(frameId, 2, 2, "Mono8", new byte[] { 1, 2, 3, 4 })
                };
            }
        }

        private sealed class ThreadProbeExecution
        {
            public string NodeId { get; set; }

            public int ThreadId { get; set; }

            public bool HasFrameVariable { get; set; }
        }

        private sealed class ThreadProbeNodeFactory : INodeFactory
        {
            public const string TypeName = "test.thread_probe";
            private readonly IList<ThreadProbeExecution> _executions;

            public ThreadProbeNodeFactory(IList<ThreadProbeExecution> executions)
            {
                _executions = executions;
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
                        DisplayName = "Thread Probe",
                        Version = "1.0.0",
                        InputPorts =
                        {
                            new NodePortDescriptor
                            {
                                Name = FlowPortNames.In,
                                DisplayName = "In",
                                Direction = FlowPortDirection.Input,
                                DataType = FlowDataType.Control
                            }
                        }
                    };
                }
            }

            public IFlowNode Create(NodeDefinition definition)
            {
                return new ThreadProbeNode(definition, _executions);
            }
        }

        private sealed class ThreadProbeNode : IFlowNode
        {
            private readonly NodeDefinition _definition;
            private readonly IList<ThreadProbeExecution> _executions;

            public ThreadProbeNode(NodeDefinition definition, IList<ThreadProbeExecution> executions)
            {
                _definition = definition;
                _executions = executions;
            }

            public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object frame;
                var execution = new ThreadProbeExecution
                {
                    NodeId = _definition.Id,
                    ThreadId = Thread.CurrentThread.ManagedThreadId,
                    HasFrameVariable = context.Variables.TryGet("hard1.Frame", out frame) && frame is CameraFrameData
                };
                lock (_executions)
                {
                    _executions.Add(execution);
                }

                return Task.FromResult(NodeExecutionResult.Success());
            }
        }
    }
}
