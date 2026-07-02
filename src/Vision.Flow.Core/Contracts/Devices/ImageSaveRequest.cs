using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 图像保存请求，描述待保存图像、目标路径和保存相关元数据。
    /// </summary>
    public sealed class ImageSaveRequest
    {
        public ImageSaveRequest()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string SaverId { get; set; }

        public IVisionImage Image { get; set; }

        public string DirectoryPath { get; set; }

        public string FileName { get; set; }

        public string Format { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
