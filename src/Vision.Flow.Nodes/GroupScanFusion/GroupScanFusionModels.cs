using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;
using static Vision.Flow.Nodes.GroupScanFusionNodeHelpers;

namespace Vision.Flow.Nodes
{
    // 共享扫描与融合模型在工业视觉节点之间传递已分组的帧数据。
    /// <summary>
    /// 由公共视觉节点生成的内存图像引用，用于 Demo、测试和占位算法输出。
    /// </summary>
    public sealed class GeneratedVisionImage : IVisionImage
    {
        public GeneratedVisionImage(string imageId, int width, int height, string pixelFormat, byte[] data)
            : this(imageId, width, height, pixelFormat, data, "Generated")
        {
        }

        public GeneratedVisionImage(string imageId, int width, int height, string pixelFormat, byte[] data, string imageKind)
        {
            ImageId = string.IsNullOrWhiteSpace(imageId) ? Guid.NewGuid().ToString("N") : imageId;
            Width = width <= 0 ? 1 : width;
            Height = height <= 0 ? 1 : height;
            PixelFormat = string.IsNullOrWhiteSpace(pixelFormat) ? "Mono8" : pixelFormat;
            ImageKind = string.IsNullOrWhiteSpace(imageKind) ? "Generated" : imageKind;
            CreatedUtc = DateTime.UtcNow;
            Data = data ?? new byte[0];
            Metadata = new Dictionary<string, object>();
            Metadata[FlowMetadataKeys.ImageKind] = ImageKind;
        }

        public string ImageId { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public string PixelFormat { get; private set; }

        public string ImageKind { get; private set; }

        public DateTime CreatedUtc { get; private set; }

        public byte[] Data { get; private set; }

        public object NativeImage
        {
            get { return null; }
        }

        public bool IsDisposed { get; private set; }

        public IDictionary<string, object> Metadata { get; private set; }

        public IVisionImage CloneReference()
        {
            var clone = new GeneratedVisionImage(ImageId, Width, Height, PixelFormat, Data, ImageKind)
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

    /// <summary>
    /// 图像组中的单次拍照结果，保留帧、图像和采集索引上下文。
    /// </summary>
    public sealed class FrameGroupItem
    {
        public FrameGroupItem()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string CaptureGroupId { get; set; }

        public int ShotIndex { get; set; }

        public CameraFrameData Frame { get; set; }

        public IVisionImage Image { get; set; }

        public string FrameId { get; set; }

        public DateTime GrabTime { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// 多点位拍照汇合后的图像组结果，供拼图和后续算法节点使用。
    /// </summary>
    public sealed class FrameGroupResult
    {
        public FrameGroupResult()
        {
            Frames = new List<FrameGroupItem>();
            Metadata = new Dictionary<string, object>();
        }

        public string CaptureGroupId { get; set; }

        public int ExpectedShotCount { get; set; }

        public int ActualShotCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime CompletedAtUtc { get; set; }

        public IList<FrameGroupItem> Frames { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// 单帧预处理结果，连接相机流式帧和扫描组汇合节点。
    /// </summary>
    public sealed class FramePreprocessResult
    {
        public FramePreprocessResult()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string ScanGroupId { get; set; }

        public int FrameIndex { get; set; }

        public CameraFrameData SourceFrame { get; set; }

        public IVisionImage SourceImage { get; set; }

        public IVisionImage PreprocessedImage { get; set; }

        public string FrameId { get; set; }

        public DateTime ProcessedAtUtc { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// 扫描组汇合结果，包含有序预处理帧及扫描级元数据。
    /// </summary>
    public sealed class ScanGroupResult
    {
        public ScanGroupResult()
        {
            Frames = new List<FramePreprocessResult>();
            Metadata = new Dictionary<string, object>();
        }

        public string ScanGroupId { get; set; }

        public int ExpectedFrameCount { get; set; }

        public int ActualFrameCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime CompletedAtUtc { get; set; }

        public IList<FramePreprocessResult> Frames { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
