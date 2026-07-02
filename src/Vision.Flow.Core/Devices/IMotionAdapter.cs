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
    /// 运控适配器接口，封装到位、等待和运动消息通知能力。
    /// </summary>
    public interface IMotionAdapter
    {
        string MotionId { get; }

        Task MoveToAsync(string positionName, CancellationToken cancellationToken);

        Task WaitForInPositionAsync(string positionName, CancellationToken cancellationToken);

        Task SendMessageAsync(MotionMessage message, CancellationToken cancellationToken);

        event EventHandler<MotionEventArgs> MotionEventReceived;
    }
}
