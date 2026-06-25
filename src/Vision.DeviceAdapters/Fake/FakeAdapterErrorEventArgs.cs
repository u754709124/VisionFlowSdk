using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // 共享 Fake 适配器错误事件数据，让模拟设备故障在测试中可观察。
    public sealed class FakeAdapterErrorEventArgs : EventArgs
    {
        public FakeAdapterErrorEventArgs(Exception exception)
        {
            Exception = exception ?? throw new ArgumentNullException("exception");
        }

        public Exception Exception { get; private set; }
    }
}
