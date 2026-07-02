using System;
using System.Collections.Generic;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Core.Runtime.Queues
{
    /// <summary>
    /// Ĭ�϶���ע���������������Դ������������ж��С�
    /// </summary>
    public sealed class FlowTaskQueueRegistry : IFlowTaskQueueRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, FlowTaskQueue> _queues;
        private readonly IFlowEventSink _eventSink;

        public FlowTaskQueueRegistry()
            : this(null)
        {
        }

        public FlowTaskQueueRegistry(IFlowEventSink eventSink)
        {
            _eventSink = eventSink ?? new InMemoryFlowEventSink();
            _queues = new Dictionary<string, FlowTaskQueue>(StringComparer.OrdinalIgnoreCase);
        }

        public FlowTaskQueue GetOrCreate(string queueName, FlowTaskQueueOptions options = null)
        {
            var normalizedName = NormalizeQueueName(queueName, options);
            lock (_gate)
            {
                FlowTaskQueue queue;
                if (_queues.TryGetValue(normalizedName, out queue))
                {
                    return queue;
                }

                var normalizedOptions = CloneOptions(options);
                normalizedOptions.QueueName = normalizedName;
                queue = new FlowTaskQueue(normalizedOptions, _eventSink);
                _queues[normalizedName] = queue;
                return queue;
            }
        }

        public bool TryGetQueue(string queueName, out FlowTaskQueue queue)
        {
            lock (_gate)
            {
                return _queues.TryGetValue(NormalizeQueueName(queueName, null), out queue);
            }
        }

        private static string NormalizeQueueName(string queueName, FlowTaskQueueOptions options)
        {
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                return queueName;
            }

            if (options != null && !string.IsNullOrWhiteSpace(options.QueueName))
            {
                return options.QueueName;
            }

            return FlowQueueNames.Default;
        }

        private static FlowTaskQueueOptions CloneOptions(FlowTaskQueueOptions options)
        {
            var source = options ?? new FlowTaskQueueOptions();
            return new FlowTaskQueueOptions
            {
                QueueName = source.QueueName,
                Capacity = source.Capacity <= 0 ? 1 : source.Capacity,
                MaxDegreeOfParallelism = source.MaxDegreeOfParallelism <= 0 ? 1 : source.MaxDegreeOfParallelism,
                FullMode = source.FullMode
            };
        }
    }
}
