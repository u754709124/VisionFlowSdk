using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
    internal sealed class CameraFrameWaitRequest
    {
        private readonly CameraFrameWaitTicket _ticket;
        private readonly TaskCompletionSource<CameraFrameData> _completion;

        public CameraFrameWaitRequest(CameraFrameWaitTicket ticket)
        {
            _ticket = ticket ?? throw new ArgumentNullException("ticket");
            _completion = new TaskCompletionSource<CameraFrameData>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<CameraFrameData> Task
        {
            get { return _completion.Task; }
        }

        public bool Matches(CameraFrameData frame)
        {
            return _ticket.Matches(frame);
        }

        public void TrySetResult(CameraFrameData frame)
        {
            _completion.TrySetResult(frame);
        }

        public void TrySetCanceled(string reason)
        {
            _completion.TrySetException(new OperationCanceledException(
                string.IsNullOrWhiteSpace(reason) ? "Camera frame wait was canceled." : reason));
        }
    }
}
