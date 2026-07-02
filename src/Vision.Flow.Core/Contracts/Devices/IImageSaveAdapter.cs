using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ͼ�񱣴��������ӿڣ�����ڵ�ֱ�����������ļ�ϵͳ��ҵ��洢�߼���
    /// </summary>
    public interface IImageSaveAdapter
    {
        string SaverId { get; }

        Task<ImageSaveResult> SaveAsync(ImageSaveRequest request, CancellationToken cancellationToken);
    }
}
