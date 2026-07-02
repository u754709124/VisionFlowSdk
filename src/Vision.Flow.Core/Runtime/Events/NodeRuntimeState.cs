using System;
using System.Threading;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// �ڵ��������¼��е�״̬���ա�
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
