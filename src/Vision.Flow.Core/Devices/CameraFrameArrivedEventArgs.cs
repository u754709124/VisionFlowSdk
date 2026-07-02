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

namespace Vision.Flow.Core.Devices
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
