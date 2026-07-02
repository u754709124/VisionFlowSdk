using System.Threading;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// 运行时有界任务队列，负责容量控制、并发限制和队列事件发布。
    /// </summary>
    public sealed partial class FlowTaskQueue
    {
        private readonly SemaphoreSlim _capacity;
        private readonly SemaphoreSlim _workers;
        private readonly IFlowEventSink _eventSink;
        private int _depth;

        public FlowTaskQueue(FlowTaskQueueOptions options, IFlowEventSink eventSink = null)
        {
            if (options == null)
            {
                options = new FlowTaskQueueOptions();
            }

            QueueName = string.IsNullOrWhiteSpace(options.QueueName) ? FlowQueueNames.Default : options.QueueName;
            Capacity = options.Capacity <= 0 ? 1 : options.Capacity;
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism <= 0 ? 1 : options.MaxDegreeOfParallelism;
            FullMode = options.FullMode;
            _capacity = new SemaphoreSlim(Capacity, Capacity);
            _workers = new SemaphoreSlim(MaxDegreeOfParallelism, MaxDegreeOfParallelism);
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
        }

        public string QueueName { get; private set; }

        public int Capacity { get; private set; }

        public int MaxDegreeOfParallelism { get; private set; }

        public FlowTaskQueueFullMode FullMode { get; private set; }

        public int CurrentDepth
        {
            get { return Volatile.Read(ref _depth); }
        }
    }
}
