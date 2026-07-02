using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 图像保存适配器接口，避免节点直接依赖具体文件系统或业务存储逻辑。
    /// </summary>
    public interface IImageSaveAdapter
    {
        string SaverId { get; }

        Task<ImageSaveResult> SaveAsync(ImageSaveRequest request, CancellationToken cancellationToken);
    }
}
