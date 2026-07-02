using System;
using System.Threading;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 节点在运行事件中的状态快照。
    /// </summary>
    public enum NodeRuntimeState
    {
        Waiting = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Timeout = 4,
        Stopped = 5
    }
}
