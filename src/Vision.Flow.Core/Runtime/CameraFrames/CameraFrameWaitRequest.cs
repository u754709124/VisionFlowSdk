using System;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;

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
