using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // Fake image savers snapshot save requests and generate predictable output paths.
    public sealed class FakeImageSaveAdapter : IImageSaveAdapter
    {
        private readonly object _gate = new object();
        private readonly List<ImageSaveRequest> _savedRequests;

        public FakeImageSaveAdapter(string saverId)
        {
            if (string.IsNullOrWhiteSpace(saverId))
            {
                throw new ArgumentException("Image saver id is required.", "saverId");
            }

            SaverId = saverId;
            BasePath = "fake://images";
            DelayMs = 0;
            _savedRequests = new List<ImageSaveRequest>();
        }

        public string SaverId { get; private set; }

        public string BasePath { get; set; }

        public int DelayMs { get; set; }

        public async Task<ImageSaveResult> SaveAsync(ImageSaveRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken).ConfigureAwait(false);
            }

            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.Image == null)
            {
                throw new ArgumentException("Image save request requires an image.", "request");
            }

            if (request.Image.IsDisposed)
            {
                throw new ObjectDisposedException("Image", "Image save request contains a disposed image.");
            }

            byte[] imageBytes;
            var hasBytes = request.Image.TryGetBytes(out imageBytes);
            var directory = string.IsNullOrWhiteSpace(request.DirectoryPath) ? BasePath : request.DirectoryPath;
            var format = string.IsNullOrWhiteSpace(request.Format) ? "png" : request.Format.TrimStart('.');
            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? request.Image.ImageId : request.FileName;
            if (fileName.IndexOf(".", StringComparison.Ordinal) < 0)
            {
                fileName = fileName + "." + format;
            }

            var result = new ImageSaveResult
            {
                IsSuccess = true,
                Path = CombinePath(directory, fileName),
                Message = "Fake image saved."
            };
            result.Metadata["SaverId"] = SaverId;
            result.Metadata["ImageId"] = request.Image.ImageId;
            result.Metadata["ByteLength"] = hasBytes && imageBytes != null ? imageBytes.Length : 0;
            result.Metadata["HasNativeImage"] = request.Image.NativeImage != null;
            result.Metadata["PixelFormat"] = request.Image.PixelFormat;
            result.Metadata["ImageKind"] = request.Image.ImageKind;

            lock (_gate)
            {
                _savedRequests.Add(CloneRequest(request));
            }

            return result;
        }

        public IList<ImageSaveRequest> SnapshotSavedRequests()
        {
            lock (_gate)
            {
                var snapshot = new List<ImageSaveRequest>();
                foreach (var request in _savedRequests)
                {
                    snapshot.Add(CloneRequest(request));
                }

                return snapshot;
            }
        }

        private static string CombinePath(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return fileName;
            }

            if (directory.EndsWith("/", StringComparison.Ordinal) || directory.EndsWith("\\", StringComparison.Ordinal))
            {
                return directory + fileName;
            }

            return directory + "/" + fileName;
        }

        private static ImageSaveRequest CloneRequest(ImageSaveRequest request)
        {
            var clone = new ImageSaveRequest
            {
                SaverId = request.SaverId,
                Image = request.Image == null ? null : request.Image.CloneReference(),
                DirectoryPath = request.DirectoryPath,
                FileName = request.FileName,
                Format = request.Format
            };

            if (request.Metadata != null)
            {
                foreach (var item in request.Metadata)
                {
                    clone.Metadata[item.Key] = item.Value;
                }
            }

            return clone;
        }
    }
}
