using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // 超时辅助对象将适配器操作超时与流程取消 Token 关联。
    internal sealed class AdapterNodeTimeout : IDisposable
    {
        private readonly CancellationTokenSource _source;

        private AdapterNodeTimeout(CancellationTokenSource source)
        {
            _source = source;
        }

        public CancellationToken Token
        {
            get { return _source.Token; }
        }

        public static AdapterNodeTimeout Create(int timeoutMs, CancellationToken cancellationToken)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMs > 0)
            {
                source.CancelAfter(timeoutMs);
            }

            return new AdapterNodeTimeout(source);
        }

        public void Dispose()
        {
            _source.Dispose();
        }
    }
}
