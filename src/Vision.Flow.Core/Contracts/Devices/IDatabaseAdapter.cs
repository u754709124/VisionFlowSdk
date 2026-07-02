using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 数据库保存适配器接口，生产上位机可在外部实现真实数据入库逻辑。
    /// </summary>
    public interface IDatabaseAdapter
    {
        string DatabaseId { get; }

        Task SaveAsync(DatabaseSaveRequest request, CancellationToken cancellationToken);
    }
}
