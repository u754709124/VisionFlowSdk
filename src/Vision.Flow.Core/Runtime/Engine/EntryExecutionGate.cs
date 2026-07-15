using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Runtime.Engine
{
    /// <summary>
    /// 入口级有界并发门控；当前只负责触发请求排队，不改变图内部调度方式。
    /// </summary>
    internal sealed class EntryExecutionGate
    {
        private readonly object _gate = new object();
        private readonly SemaphoreSlim _slots;
        private readonly int _queueCapacity;
        private int _waitingCount;

        public EntryExecutionGate(TriggerExecutionPolicy policy)
        {
            var effective = policy ?? new TriggerExecutionPolicy();
            var maxConcurrentRuns = effective.MaxConcurrentRuns <= 0 ? 1 : effective.MaxConcurrentRuns;
            _queueCapacity = effective.QueueCapacity < 0 ? 0 : effective.QueueCapacity;
            _slots = new SemaphoreSlim(maxConcurrentRuns, maxConcurrentRuns);
        }

        public async Task<EntryExecutionLease> TryEnterAsync(CancellationToken cancellationToken)
        {
            if (_slots.Wait(0))
            {
                return new EntryExecutionLease(_slots);
            }

            lock (_gate)
            {
                if (_waitingCount >= _queueCapacity)
                {
                    return null;
                }

                _waitingCount++;
            }

            try
            {
                await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new EntryExecutionLease(_slots);
            }
            finally
            {
                lock (_gate)
                {
                    _waitingCount--;
                }
            }
        }
    }

    /// <summary>
    /// 入口并发槽租约，保证取消、失败和成功路径都只释放一次。
    /// </summary>
    internal sealed class EntryExecutionLease : IDisposable
    {
        private SemaphoreSlim _slots;

        public EntryExecutionLease(SemaphoreSlim slots)
        {
            _slots = slots;
        }

        public void Dispose()
        {
            var slots = Interlocked.Exchange(ref _slots, null);
            if (slots != null)
            {
                slots.Release();
            }
        }
    }

    public sealed partial class FlowRunner
    {
        private static Dictionary<string, EntryExecutionGate> CreateEntryGates(IList<FlowEntryDefinition> entries)
        {
            var result = new Dictionary<string, EntryExecutionGate>(StringComparer.OrdinalIgnoreCase);
            if (entries == null)
            {
                return result;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && !string.IsNullOrWhiteSpace(entry.EntryName) && !result.ContainsKey(entry.EntryName))
                {
                    result[entry.EntryName] = new EntryExecutionGate(entry.ExecutionPolicy);
                }
            }

            return result;
        }

        private EntryExecutionGate GetEntryGate(FlowEntryDefinition entry)
        {
            EntryExecutionGate entryGate;
            if (!_entryGates.TryGetValue(entry.EntryName, out entryGate))
            {
                lock (_gate)
                {
                    if (!_entryGates.TryGetValue(entry.EntryName, out entryGate))
                    {
                        entryGate = new EntryExecutionGate(entry.ExecutionPolicy);
                        _entryGates[entry.EntryName] = entryGate;
                    }
                }
            }

            return entryGate;
        }
    }
}
