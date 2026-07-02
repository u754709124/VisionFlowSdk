using System;
using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// �Ӿ�ͼ��������� Fake ͼ��SDK ԭ��ͼ�����úͺ�������ͳһ��ʽ��ת��
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
