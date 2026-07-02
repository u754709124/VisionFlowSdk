using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ��Դ�������ӿڣ��ڵ�ͨ��������ͨ�����Ⱥ͹رչ�Դ��
    /// </summary>
    public interface ILightAdapter
    {
        string LightId { get; }

        Task SetAsync(LightChannelSetting setting, CancellationToken cancellationToken);

        Task TurnOffAsync(string channelName, CancellationToken cancellationToken);
    }
}
