using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 光源适配器接口，节点通过它设置通道亮度和关闭光源。
    /// </summary>
    public interface ILightAdapter
    {
        string LightId { get; }

        Task SetAsync(LightChannelSetting setting, CancellationToken cancellationToken);

        Task TurnOffAsync(string channelName, CancellationToken cancellationToken);
    }
}
