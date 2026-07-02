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
    /// 视觉图像抽象，允许 Fake 图像、SDK 原生图像引用和后处理结果以统一方式流转。
    /// </summary>
    public interface IVisionImage : IDisposable
    {
        string ImageId { get; }

        int Width { get; }

        int Height { get; }

        string PixelFormat { get; }

        string ImageKind { get; }

        DateTime CreatedUtc { get; }

        byte[] Data { get; }

        object NativeImage { get; }

        bool IsDisposed { get; }

        IDictionary<string, object> Metadata { get; }

        IVisionImage CloneReference();

        bool TryGetBytes(out byte[] data);
    }
}
