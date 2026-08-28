using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Tests
{
    internal static class FlowEventSinkTests
    {
        /// <summary>
        /// 验证旧二进制调用方依赖的无参构造器仍真实存在于程序集元数据。
        /// </summary>
        public static Task ParameterlessInMemorySinkPreservesBinaryContract()
        {
            var constructor = typeof(InMemoryFlowEventSink).GetConstructor(
                Type.EmptyTypes);
            AssertEx.True(
                constructor != null,
                "The legacy public parameterless constructor must remain present in metadata.");
            return Task.FromResult(0);
        }

        /// <summary>
        /// 验证事件快照不会保留图像、帧或二进制缓冲区资源。
        /// </summary>
        public static async Task SanitizerRemovesResourceReferences()
        {
            var inner = new InMemoryFlowEventSink(16);
            var sink = new SanitizingFlowEventSink(inner);
            var image = new VisionImageReference("image-001", 8, 6, "Mono8", new byte[1024]);
            var frame = new CameraFrameData
            {
                CameraId = "camera-1",
                CaptureFrameId = "frame-1",
                Image = image
            };
            var runtimeEvent = new FlowRuntimeEvent
            {
                EventType = FlowRuntimeEventType.OutputProduced,
                Data =
                {
                    { FlowRuntimeDataKeys.Value, image },
                    { "Frame", frame },
                    { "Bytes", new byte[4096] }
                }
            };

            await sink.PublishAsync(runtimeEvent, CancellationToken.None).ConfigureAwait(false);
            var snapshot = inner.Events.Single();

            AssertEx.True(snapshot.Data[FlowRuntimeDataKeys.Value] is FlowRuntimeValueSummary, "Image values must become lightweight summaries.");
            var frameSnapshot = snapshot.Data["Frame"] as FlowRuntimeObjectSnapshot;
            AssertEx.True(frameSnapshot != null, "Camera frames must expose a typed member snapshot.");
            AssertEx.Equal(typeof(CameraFrameData).FullName, frameSnapshot.TypeName, "Camera frame snapshots must preserve the actual CLR type.");
            AssertEx.Equal("camera-1", Convert.ToString(frameSnapshot.Members["CameraId"]), "Camera frame metadata getters must remain inspectable.");
            AssertEx.Equal("frame-1", Convert.ToString(frameSnapshot.Members["CaptureFrameId"]), "Camera frame identifier getters must remain inspectable.");
            AssertEx.True(frameSnapshot.Members["Image"] is FlowRuntimeValueSummary, "Only the nested image resource must remain summarized.");
            AssertEx.True(snapshot.Data["Bytes"] is FlowRuntimeValueSummary, "Binary payloads must become lightweight summaries.");
            AssertEx.False(ReferenceEquals(snapshot.Data[FlowRuntimeDataKeys.Value], image), "The event snapshot must not retain the image object.");
            AssertEx.False(ReferenceEquals(snapshot.Data["Frame"], frame), "The event snapshot must not retain the camera frame.");
            image.Dispose();
        }

        /// <summary>
        /// 验证普通对象按公开实例成员形成独立快照，同时隔离异常 getter、循环引用和资源成员。
        /// </summary>
        public static async Task SanitizerCapturesPublicObjectMembersSafely()
        {
            var inner = new InMemoryFlowEventSink(16);
            var sink = new SanitizingFlowEventSink(
                inner,
                new FlowEventSinkOptions
                {
                    MaxDataDepth = 4,
                    MaxCollectionItems = 16,
                    MaxStringLength = 64
                });
            var source = new DerivedSnapshotProbe
            {
                Name = "property-value",
                Inherited = "inherited-value",
                PublicField = 42,
                Nested = new NestedSnapshotProbe { Enabled = true },
                Resource = new DisposableSnapshotProbe(),
                InspectableResource = new InspectableDisposableSnapshotProbe { ResourceId = "resource-1" },
                DictionaryValues = new ReadOnlyDictionary<string, object>(
                    new Dictionary<string, object>
                    {
                        { "Number", 9 },
                        { "Nested", new NestedSnapshotProbe { Enabled = true } },
                        { "Tuple", new HalconDotNet.HTuple() }
                    })
            };
            source.NestedField = new NestedSnapshotProbe { Enabled = false };
            source.Self = source;
            DerivedSnapshotProbe.ThrowingGetterCallCount = 0;
            DerivedSnapshotProbe.DeclaredTupleGetterCallCount = 0;
            DerivedSnapshotProbe.RuntimeTupleGetterCallCount = 0;

            await sink.PublishAsync(
                new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.OutputProduced,
                    Data = { { FlowRuntimeDataKeys.Value, source } }
                },
                CancellationToken.None).ConfigureAwait(false);

            var objectSnapshot = inner.Events.Single().Data[FlowRuntimeDataKeys.Value] as FlowRuntimeObjectSnapshot;
            AssertEx.True(objectSnapshot != null, "Ordinary objects must become typed structured member snapshots.");
            AssertEx.Equal(typeof(DerivedSnapshotProbe).FullName, objectSnapshot.TypeName, "Object snapshots must preserve the actual CLR type.");
            IDictionary<string, object> snapshot = objectSnapshot.Members;
            AssertEx.Equal("property-value", Convert.ToString(snapshot["Name"]), "A public property must win over an inherited field with the same name.");
            AssertEx.Equal("inherited-value", Convert.ToString(snapshot["Inherited"]), "Inherited public members must be captured.");
            AssertEx.Equal(42, Convert.ToInt32(snapshot["PublicField"]), "Public fields must be captured.");
            AssertEx.False(snapshot.ContainsKey("PrivateValue"), "Private members must not be captured.");
            AssertEx.False(snapshot.ContainsKey("StaticValue"), "Static members must not be captured.");
            AssertEx.False(snapshot.ContainsKey("Item"), "Indexer properties must not be captured.");
            AssertEx.True(Convert.ToString(snapshot["Throwing"]).StartsWith("<读取失败: InvalidOperationException>"), "A failing getter must become a stable placeholder.");
            AssertEx.Equal(1, DerivedSnapshotProbe.ThrowingGetterCallCount, "A normal debug snapshot must evaluate each public getter once.");
            AssertEx.True(snapshot["Self"] is FlowRuntimeValueSummary, "A cyclic property value must become a summary.");
            AssertEx.True(snapshot["Resource"] is FlowRuntimeValueSummary, "A disposable child without public metadata must remain a resource summary.");
            var inspectableResource = snapshot["InspectableResource"] as FlowRuntimeObjectSnapshot;
            AssertEx.True(inspectableResource != null && inspectableResource.IsResource, "Disposable wrappers with public metadata must expose a typed non-owning resource snapshot.");
            AssertEx.Equal("resource-1", Convert.ToString(inspectableResource.Members["ResourceId"]), "Inspectable resource metadata getters must remain visible.");
            var nested = snapshot["Nested"] as FlowRuntimeObjectSnapshot;
            AssertEx.True(nested != null && Convert.ToBoolean(nested.Members["Enabled"]), "Nested object getters must remain expandable with their actual type.");
            var nestedField = snapshot["NestedField"] as FlowRuntimeObjectSnapshot;
            AssertEx.True(nestedField != null && !Convert.ToBoolean(nestedField.Members["Enabled"]),
                "Public fields and properties inside them must remain expandable.");
            var declaredTuple = snapshot["Tuple"] as FlowRuntimeUnevaluatedValue;
            AssertEx.True(declaredTuple != null && declaredTuple.Reason == "HTupleNotEvaluated",
                "A property declared as HTuple must terminate without evaluation.");
            AssertEx.Equal(0, DerivedSnapshotProbe.DeclaredTupleGetterCallCount,
                "A property declared as HTuple must not invoke its getter.");
            var runtimeTuple = snapshot["RuntimeTuple"] as FlowRuntimeUnevaluatedValue;
            AssertEx.True(runtimeTuple != null && runtimeTuple.TypeName == typeof(HalconDotNet.HTuple).FullName,
                "An HTuple returned through object must terminate before reflecting HTuple members.");
            AssertEx.Equal(1, DerivedSnapshotProbe.RuntimeTupleGetterCallCount,
                "An object-typed getter must be evaluated once before its runtime HTuple type can be identified.");
            var dictionaryValues = snapshot["DictionaryValues"] as IDictionary<string, object>;
            AssertEx.Equal(9, Convert.ToInt32(dictionaryValues["Number"]),
                "A dictionary returned by a getter must expose scalar values.");
            AssertEx.True(dictionaryValues["Nested"] is FlowRuntimeObjectSnapshot,
                "A dictionary returned by a getter must recursively expose ordinary object values.");
            AssertEx.True(dictionaryValues["Tuple"] is FlowRuntimeUnevaluatedValue,
                "An HTuple stored in a dictionary must terminate only at that value.");
        }

        /// <summary>验证结构体以及字典、列表中的对象元素同样保留真实类型。</summary>
        public static async Task SanitizerPreservesTypesAcrossObjectShapes()
        {
            var inner = new InMemoryFlowEventSink(16);
            var sink = new SanitizingFlowEventSink(inner);
            await sink.PublishAsync(
                new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.OutputProduced,
                    Data =
                    {
                        { "Struct", new SnapshotStructProbe { Count = 3 } },
                        { "List", new List<object> { new NestedSnapshotProbe { Enabled = true } } },
                        { "Dictionary", new Dictionary<string, object> { { "item", new NestedSnapshotProbe { Enabled = false } } } }
                    }
                },
                CancellationToken.None).ConfigureAwait(false);

            FlowRuntimeEvent snapshot = inner.Events.Single();
            var structValue = snapshot.Data["Struct"] as FlowRuntimeObjectSnapshot;
            AssertEx.True(structValue != null, "Custom structs must become typed snapshots.");
            AssertEx.Equal(typeof(SnapshotStructProbe).FullName, structValue.TypeName, "Custom structs must preserve their actual type.");
            AssertEx.Equal(3, Convert.ToInt32(structValue.Members["Count"]), "Custom struct fields must remain inspectable.");
            var list = snapshot.Data["List"] as IList<object>;
            var listItem = list[0] as FlowRuntimeObjectSnapshot;
            AssertEx.Equal(typeof(NestedSnapshotProbe).FullName, listItem.TypeName, "List elements must preserve their actual type.");
            AssertEx.True(Convert.ToBoolean(listItem.Members["Enabled"]), "List element property getters must remain inspectable.");
            var dictionary = snapshot.Data["Dictionary"] as IDictionary<string, object>;
            var dictionaryItem = dictionary["item"] as FlowRuntimeObjectSnapshot;
            AssertEx.Equal(typeof(NestedSnapshotProbe).FullName, dictionaryItem.TypeName, "Dictionary values must preserve their actual type.");
            AssertEx.False(Convert.ToBoolean(dictionaryItem.Members["Enabled"]), "Dictionary value property getters must remain inspectable.");
        }

        /// <summary>验证默认五层递归可以穿过节点输入协议包装并展开业务对象及集合元素。</summary>
        public static async Task SanitizerDefaultDepthExpandsCapturedInputValues()
        {
            var inner = new InMemoryFlowEventSink(16);
            var options = new FlowEventSinkOptions();
            AssertEx.Equal(5, options.MaxDataDepth,
                "The Core snapshot depth must stay aligned with the five-level debugger tree.");
            var sink = new SanitizingFlowEventSink(inner, options);
            var inputValue = new CapturedInputProbe
            {
                Context = new NestedSnapshotProbe { Enabled = true },
                Frames = new List<NestedSnapshotProbe>
                {
                    new NestedSnapshotProbe { Enabled = false }
                },
                Metadata = new Dictionary<string, object>
                {
                    { "Station", "A" }
                }
            };
            await sink.PublishAsync(
                new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.NodeInputsCaptured,
                    Data =
                    {
                        {
                            FlowRuntimeDataKeys.Inputs,
                            new List<IDictionary<string, object>>
                            {
                                new Dictionary<string, object>
                                {
                                    { FlowRuntimeDataKeys.SettingName, "Batch" },
                                    { FlowRuntimeDataKeys.Value, inputValue }
                                }
                            }
                        }
                    }
                },
                CancellationToken.None).ConfigureAwait(false);

            var inputs = inner.Events.Single().Data[FlowRuntimeDataKeys.Inputs] as IList<object>;
            var input = inputs[0] as IDictionary<string, object>;
            var value = input[FlowRuntimeDataKeys.Value] as FlowRuntimeObjectSnapshot;
            AssertEx.True(value != null, "The captured input value must remain a typed object after protocol wrappers.");
            AssertEx.True(value.Members["Context"] is FlowRuntimeObjectSnapshot,
                "A captured input context within the five-level limit must remain expandable.");
            var frames = value.Members["Frames"] as IList<object>;
            AssertEx.True(frames != null && frames[0] is FlowRuntimeObjectSnapshot,
                "Collection elements within the five-level limit must remain expandable.");
            var metadata = value.Members["Metadata"] as IDictionary<string, object>;
            AssertEx.Equal("A", Convert.ToString(metadata["Station"]),
                "Dictionaries within the five-level limit must remain expandable.");
        }

        /// <summary>
        /// 验证对象成员快照复用集合数量与递归深度上限。
        /// </summary>
        public static async Task SanitizerAppliesObjectMemberLimits()
        {
            var memberInner = new InMemoryFlowEventSink(16);
            var memberSink = new SanitizingFlowEventSink(
                memberInner,
                new FlowEventSinkOptions
                {
                    MaxDataDepth = 4,
                    MaxCollectionItems = 2
                });
            var source = new DerivedSnapshotProbe
            {
                Name = "name",
                Inherited = "inherited",
                PublicField = 7,
                Nested = new NestedSnapshotProbe { Enabled = true }
            };
            source.NestedField = new NestedSnapshotProbe { Enabled = true };

            await memberSink.PublishAsync(
                new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.OutputProduced,
                    Data = { { FlowRuntimeDataKeys.Value, source } }
                },
                CancellationToken.None).ConfigureAwait(false);

            var memberSnapshot = memberInner.Events.Single().Data[FlowRuntimeDataKeys.Value] as FlowRuntimeObjectSnapshot;
            AssertEx.True(memberSnapshot != null, "The root object must still be structured within the configured depth.");
            AssertEx.Equal(2, memberSnapshot.Members.Count, "MaxCollectionItems must cap public object members.");

            var depthInner = new InMemoryFlowEventSink(16);
            var depthSink = new SanitizingFlowEventSink(
                depthInner,
                new FlowEventSinkOptions
                {
                    MaxDataDepth = 1,
                    MaxCollectionItems = 16
                });
            await depthSink.PublishAsync(
                new FlowRuntimeEvent
                {
                    EventType = FlowRuntimeEventType.OutputProduced,
                    Data = { { FlowRuntimeDataKeys.Value, source } }
                },
                CancellationToken.None).ConfigureAwait(false);

            var depthSnapshot = depthInner.Events.Single().Data[FlowRuntimeDataKeys.Value] as FlowRuntimeObjectSnapshot;
            AssertEx.True(depthSnapshot != null && depthSnapshot.Members["NestedField"] is FlowRuntimeValueSummary, "Nested field values at the configured depth boundary must become summaries.");
        }

        /// <summary>
        /// 验证十万条遥测压力下队列保持有界，且关键终态通过背压送达而不被丢弃。
        /// </summary>
        public static async Task BoundedSinkContainsTelemetryPressure()
        {
            const int capacity = 16;
            var downstream = new BlockingEventSink();
            var sink = new BoundedFlowEventSink(
                downstream,
                new FlowEventSinkOptions
                {
                    Capacity = capacity,
                    OverflowPolicy = FlowEventOverflowPolicy.DropOldest
                });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = GC.GetTotalMemory(true);
            for (var index = 0; index < 100000; index++)
            {
                await sink.PublishAsync(
                    new FlowRuntimeEvent
                    {
                        EventType = FlowRuntimeEventType.OutputProduced,
                        FlowRunId = "pressure-" + index,
                        Data = { { FlowRuntimeDataKeys.Value, index } }
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }

            AssertEx.True(sink.QueuedEventCount <= capacity, "The event queue must never exceed its configured capacity.");
            AssertEx.True(sink.DroppedEventCount > 0, "Telemetry overflow must be observable through the dropped counter.");

            var terminal = new FlowRuntimeEvent
            {
                EventType = FlowRuntimeEventType.FlowRunCompleted,
                FlowRunId = "critical-terminal"
            };
            var terminalTask = sink.PublishAsync(terminal, CancellationToken.None);
            AssertEx.False(terminalTask.IsCompleted, "A critical terminal must wait for capacity instead of being dropped.");

            downstream.Release();
            await terminalTask.ConfigureAwait(false);
            await sink.FlushAsync().ConfigureAwait(false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var retainedBytes = Math.Max(0, GC.GetTotalMemory(true) - before);

            AssertEx.True(
                downstream.Events.Any(x =>
                    x.EventType == FlowRuntimeEventType.FlowRunCompleted &&
                    x.FlowRunId == "critical-terminal"),
                "The critical terminal event must reach the downstream sink.");
            AssertEx.True(retainedBytes <= 32L * 1024L * 1024L, "One hundred thousand telemetry events must retain no more than 32 MB.");
            sink.Dispose();
        }

        private sealed class BlockingEventSink : IFlowEventSink
        {
            private readonly object _gate = new object();
            private readonly List<FlowRuntimeEvent> _events = new List<FlowRuntimeEvent>();
            private readonly TaskCompletionSource<object> _release =
                new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public IList<FlowRuntimeEvent> Events
            {
                get
                {
                    lock (_gate)
                    {
                        return new List<FlowRuntimeEvent>(_events);
                    }
                }
            }

            public async Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
            {
                await _release.Task.ConfigureAwait(false);
                lock (_gate)
                {
                    _events.Add(runtimeEvent);
                }
            }

            public void Release()
            {
                _release.TrySetResult(null);
            }
        }

        private class BaseSnapshotProbe
        {
            public string Name = "base-field";

            public string Inherited;

            public static string StaticValue { get { return "static"; } }

            private string PrivateValue { get { return "private"; } }

            public string this[int index] { get { return index.ToString(); } }
        }

        private sealed class DerivedSnapshotProbe : BaseSnapshotProbe
        {
            public new string Name { get; set; }

            public int PublicField;

            public NestedSnapshotProbe Nested { get; set; }

            public NestedSnapshotProbe NestedField;

            public DerivedSnapshotProbe Self { get; set; }

            public IDisposable Resource { get; set; }

            public IDisposable InspectableResource { get; set; }

            public static int ThrowingGetterCallCount;

            public static int DeclaredTupleGetterCallCount;

            public static int RuntimeTupleGetterCallCount;

            public HalconDotNet.HTuple Tuple
            {
                get
                {
                    DeclaredTupleGetterCallCount++;
                    return new HalconDotNet.HTuple();
                }
            }

            public object RuntimeTuple
            {
                get
                {
                    RuntimeTupleGetterCallCount++;
                    return new HalconDotNet.HTuple();
                }
            }

            public IReadOnlyDictionary<string, object> DictionaryValues { get; set; }

            public string Throwing
            {
                get
                {
                    ThrowingGetterCallCount++;
                    throw new InvalidOperationException("probe");
                }
            }
        }

        private sealed class NestedSnapshotProbe
        {
            public bool Enabled { get; set; }
        }

        private sealed class CapturedInputProbe
        {
            public NestedSnapshotProbe Context;

            public IList<NestedSnapshotProbe> Frames;

            public IDictionary<string, object> Metadata;
        }

        private sealed class DisposableSnapshotProbe : IDisposable
        {
            public void Dispose()
            {
            }
        }

        private sealed class InspectableDisposableSnapshotProbe : IDisposable
        {
            public string ResourceId { get; set; }

            public void Dispose()
            {
            }
        }

        private struct SnapshotStructProbe
        {
            public int Count;
        }
    }
}

namespace HalconDotNet
{
    public sealed class HTuple
    {
        public string DangerousGetter
        {
            get { throw new InvalidOperationException("HTuple members must never be reflected."); }
        }
    }
}
