using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Runtime.Events
{
    public sealed class InMemoryFlowEventSink : IFlowEventSink
    {
        private readonly object _gate = new object();
        private readonly List<FlowRuntimeEvent> _events = new List<FlowRuntimeEvent>();
        private readonly int _capacity;
        private long _droppedEventCount;

        /// <summary>
        /// 使用 4096 条容量创建有界内存事件出口，保留旧调用方的无参二进制契约。
        /// </summary>
        public InMemoryFlowEventSink()
            : this(4096)
        {
        }

        /// <summary>
        /// 使用指定容量创建有界内存事件出口，容量满时淘汰最早事件。
        /// </summary>
        /// <param name="capacity">允许保留的最大事件数量。</param>
        public InMemoryFlowEventSink(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException("capacity");
            }

            _capacity = capacity;
        }

        /// <summary>
        /// 获取因容量限制而淘汰的事件总数。
        /// </summary>
        public long DroppedEventCount
        {
            get { return Interlocked.Read(ref _droppedEventCount); }
        }

        /// <summary>
        /// 获取当前保留事件的线程安全快照。
        /// </summary>
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

        /// <summary>
        /// 保存事件；容量满时先淘汰最早事件。
        /// </summary>
        public Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            if (runtimeEvent == null)
            {
                throw new ArgumentNullException("runtimeEvent");
            }

            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_events.Count == _capacity)
                {
                    _events.RemoveAt(0);
                    Interlocked.Increment(ref _droppedEventCount);
                }

                _events.Add(runtimeEvent);
            }

            return Task.FromResult(0);
        }
    }
}
