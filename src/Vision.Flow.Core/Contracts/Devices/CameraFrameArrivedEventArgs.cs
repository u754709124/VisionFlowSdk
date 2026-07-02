using System;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ���֡�����¼����������ٷ�װ�ص�֡�󽻸�����ʱ·�ɡ�
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
