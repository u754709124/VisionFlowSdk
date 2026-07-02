using System;
using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
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
