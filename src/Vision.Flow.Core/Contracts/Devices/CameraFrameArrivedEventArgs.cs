using System;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 相机帧到达事件参数，快速封装回调帧后交给运行时路由。
    /// </summary>
    public sealed class CameraFrameArrivedEventArgs : EventArgs
    {
        public CameraFrameArrivedEventArgs(CameraFrameData frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            Frame = frame;
        }

        public CameraFrameData Frame { get; private set; }
    }
}
