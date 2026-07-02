using System;
using Vision.Flow.Core.Contracts.Devices;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
    internal sealed class BufferedCameraFrame
    {
        public BufferedCameraFrame(CameraFrameData frame, DateTime arrivedUtc)
        {
            Frame = frame;
            ArrivedUtc = arrivedUtc;
        }

        public CameraFrameData Frame { get; private set; }

        public DateTime ArrivedUtc { get; private set; }
    }
}
