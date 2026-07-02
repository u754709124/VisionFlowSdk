using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ͼ�񱣴���������������ͼ��Ŀ��·���ͱ������Ԫ���ݡ�
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
