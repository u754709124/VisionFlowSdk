using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // Shared fake-adapter error event data keeps simulated device failures observable in tests.
    public sealed class FakeAdapterErrorEventArgs : EventArgs
    {
        public FakeAdapterErrorEventArgs(Exception exception)
        {
            Exception = exception ?? throw new ArgumentNullException("exception");
        }

        public Exception Exception { get; private set; }
    }
}
