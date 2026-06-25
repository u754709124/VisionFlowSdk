using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // Timeout helper links node-level timeout with the flow cancellation token.
    internal sealed class CameraNodeTimeout : IDisposable
    {
        private readonly CancellationTokenSource _source;

        private CameraNodeTimeout(CancellationTokenSource source)
        {
            _source = source;
        }

        public CancellationToken Token
        {
            get { return _source.Token; }
        }

        public static CameraNodeTimeout Create(int timeoutMs, CancellationToken cancellationToken)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMs > 0)
            {
                source.CancelAfter(timeoutMs);
            }

            return new CameraNodeTimeout(source);
        }

        public void Dispose()
        {
            _source.Dispose();
        }
    }
}
