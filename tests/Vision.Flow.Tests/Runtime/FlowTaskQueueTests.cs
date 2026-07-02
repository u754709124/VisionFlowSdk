using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Nodes;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // 队列测试覆盖重型适配器节点使用的有界后台执行行为。
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
                sink.Events.Any(x => string.Equals(Convert.ToString(x.Data[FlowRuntimeDataKeys.QueueName]), "save", StringComparison.OrdinalIgnoreCase)),
                "Queue events should include queue name.");
        }

        public static async Task DropStopAndNotifyFullModes()
        {
            var drop = await FillAndTrySecondAsync(FlowTaskQueueFullMode.Drop).ConfigureAwait(false);
            AssertEx.True(drop.Result.IsDropped, "Drop mode should mark the second item as dropped.");
            AssertEx.False(drop.Result.IsAccepted, "Dropped queue work should not be accepted.");
            AssertEx.True(
                drop.Sink.Events.Any(x => x.EventType == FlowRuntimeEventType.QueueWarning),
                "Drop mode should publish a queue warning.");

            var stop = await FillAndTrySecondAsync(FlowTaskQueueFullMode.StopFlow).ConfigureAwait(false);
            AssertEx.True(stop.Result.ShouldStopFlow, "StopFlow mode should request flow stop.");
            AssertEx.True(stop.Result.IsRejected, "StopFlow mode should reject the full item.");
            AssertEx.True(
                stop.Sink.Events.Any(x => x.EventType == FlowRuntimeEventType.QueueRejected),
                "StopFlow mode should publish QueueRejected.");

            var notify = await FillAndTrySecondAsync(FlowTaskQueueFullMode.NotifyOnly).ConfigureAwait(false);
            AssertEx.True(notify.Result.IsNotifyOnly, "NotifyOnly mode should mark notification-only full items.");
            AssertEx.False(notify.Result.IsAccepted, "NotifyOnly full items should not execute work.");
            AssertEx.True(
                notify.Sink.Events.Any(x => x.EventType == FlowRuntimeEventType.QueueWarning),
                "NotifyOnly mode should publish a queue warning.");
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

        private static async Task<FullModeAttempt> FillAndTrySecondAsync(FlowTaskQueueFullMode mode)
        {
            var sink = new InMemoryFlowEventSink();
            var queue = new FlowTaskQueue(
                new FlowTaskQueueOptions
                {
                    QueueName = "full-" + mode.ToString().ToLowerInvariant(),
                    Capacity = 1,
                    MaxDegreeOfParallelism = 1,
                    FullMode = mode
                },
                sink);
            var release = new TaskCompletionSource<int>();
            var firstStarted = new TaskCompletionSource<int>();
            var context = new FlowTaskQueueItemContext
            {
                FlowId = "flow-full",
                TokenId = "token-full",
                NodeId = "queue1",
                OperationName = "FullMode"
            };

            var first = queue.EnqueueAsync<int>(
                async delegate(CancellationToken token)
                {
                    firstStarted.TrySetResult(0);
                    await release.Task.ConfigureAwait(false);
                    return 1;
                },
                context,
                CancellationToken.None);

            await firstStarted.Task.ConfigureAwait(false);
            var second = await queue.EnqueueAsync<int>(
                delegate(CancellationToken token)
                {
                    return Task.FromResult(2);
                },
                context,
                CancellationToken.None).ConfigureAwait(false);

            release.TrySetResult(0);
            await first.ConfigureAwait(false);
            return new FullModeAttempt
            {
                Result = second,
                Sink = sink
            };
        }

        private sealed class FullModeAttempt
        {
            public FlowTaskQueueResult<int> Result { get; set; }

            public InMemoryFlowEventSink Sink { get; set; }
        }
    }
}
