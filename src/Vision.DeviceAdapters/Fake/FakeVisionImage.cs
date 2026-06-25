using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // Fake images provide cloneable in-memory image references for offline tests and demos.
    public sealed class FakeVisionImage : IVisionImage
    {
        private readonly bool _ownsNativeImage;

        public FakeVisionImage()
            : this(null, 640, 480, "Mono8", null)
        {
        }

        public FakeVisionImage(string imageId, int width, int height, string pixelFormat, byte[] data)
            : this(imageId, width, height, pixelFormat, data, null, false)
        {
        }

        public FakeVisionImage(string imageId, int width, int height, string pixelFormat, byte[] data, object nativeImage)
            : this(imageId, width, height, pixelFormat, data, nativeImage, false)
        {
        }

        public FakeVisionImage(
            string imageId,
            int width,
            int height,
            string pixelFormat,
            byte[] data,
            object nativeImage,
            bool ownsNativeImage)
            : this(imageId, width, height, pixelFormat, data, nativeImage, ownsNativeImage, "Raw")
        {
        }

        public FakeVisionImage(
            string imageId,
            int width,
            int height,
            string pixelFormat,
            byte[] data,
            object nativeImage,
            bool ownsNativeImage,
            string imageKind)
        {
            ImageId = string.IsNullOrWhiteSpace(imageId) ? Guid.NewGuid().ToString("N") : imageId;
            Width = width <= 0 ? 1 : width;
            Height = height <= 0 ? 1 : height;
            PixelFormat = string.IsNullOrWhiteSpace(pixelFormat) ? "Mono8" : pixelFormat;
            ImageKind = string.IsNullOrWhiteSpace(imageKind) ? "Raw" : imageKind;
            CreatedUtc = DateTime.UtcNow;
            Data = data ?? new byte[0];
            NativeImage = nativeImage;
            _ownsNativeImage = ownsNativeImage;
            Metadata = new Dictionary<string, object>();
        }

        public string ImageId { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public string PixelFormat { get; private set; }

        public string ImageKind { get; private set; }

        public DateTime CreatedUtc { get; private set; }

        public byte[] Data { get; private set; }

        public object NativeImage { get; private set; }

        public bool IsDisposed { get; private set; }

        public IDictionary<string, object> Metadata { get; private set; }

        public IVisionImage CloneReference()
        {
            var clone = new FakeVisionImage(ImageId, Width, Height, PixelFormat, Data, NativeImage, false, ImageKind)
            {
                CreatedUtc = CreatedUtc
            };
            CopyMetadata(Metadata, clone.Metadata);
            return clone;
        }

        public bool TryGetBytes(out byte[] data)
        {
            data = null;
            if (IsDisposed || Data == null)
            {
                return false;
            }

            data = new byte[Data.Length];
            if (Data.Length > 0)
            {
                Buffer.BlockCopy(Data, 0, data, 0, Data.Length);
            }

            return true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            if (_ownsNativeImage)
            {
                var disposable = NativeImage as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }

            NativeImage = null;
            Data = new byte[0];
        }

        private static void CopyMetadata(IDictionary<string, object> source, IDictionary<string, object> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (var item in source)
            {
                target[item.Key] = item.Value;
            }
        }
    }
}
