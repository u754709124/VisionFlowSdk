using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ���ݿⱣ���������ӿڣ�������λ�������ⲿʵ����ʵ��������߼���
    /// </summary>
    public interface IDatabaseAdapter
    {
        string DatabaseId { get; }

        Task SaveAsync(DatabaseSaveRequest request, CancellationToken cancellationToken);
    }
}
