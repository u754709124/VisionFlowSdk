using System.Collections.Generic;
using System.IO;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 图像保存结果，向流程返回保存状态、路径和附加元数据。
    /// </summary>
    public sealed class ImageSaveResult
    {
        public ImageSaveResult()
        {
            Metadata = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public string Path { get; set; }

        public string Message { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }
}
