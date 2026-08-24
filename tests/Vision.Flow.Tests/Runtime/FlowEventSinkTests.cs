using System;
using System.Collections.Generic;
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
                FrameId = "frame-1",
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
            AssertEx.True(snapshot.Data["Frame"] is FlowRuntimeValueSummary, "Camera frames must become lightweight summaries.");
            AssertEx.True(snapshot.Data["Bytes"] is FlowRuntimeValueSummary, "Binary payloads must become lightweight summaries.");
            AssertEx.False(ReferenceEquals(snapshot.Data[FlowRuntimeDataKeys.Value], image), "The event snapshot must not retain the image object.");
            AssertEx.False(ReferenceEquals(snapshot.Data["Frame"], frame), "The event snapshot must not retain the camera frame.");
            image.Dispose();
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
    }
}
