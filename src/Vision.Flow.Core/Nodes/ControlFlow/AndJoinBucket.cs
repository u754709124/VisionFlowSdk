using System;
using System.Collections.Generic;

namespace Vision.Flow.Nodes
{
    internal sealed class AndJoinBucket
    {
        public AndJoinBucket(string joinKey, int expectedInputCount, int timeoutMs)
        {
            JoinKey = joinKey;
            ExpectedInputCount = expectedInputCount;
            TimeoutMs = timeoutMs;
            CreatedAtUtc = DateTime.UtcNow;
            Inputs = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        public string JoinKey { get; private set; }

        public int ExpectedInputCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; private set; }

        public Dictionary<string, DateTime> Inputs { get; private set; }

        public bool IsExpired()
        {
            return TimeoutMs > 0 && DateTime.UtcNow - CreatedAtUtc > TimeSpan.FromMilliseconds(TimeoutMs);
        }
    }
}
