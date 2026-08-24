using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 定义单个物理光源控制器的协议无关 Adapter；设备连接生命周期由宿主管理。
    /// </summary>
    public interface ILightControllerAdapter
    {
        /// <summary>获取设备配置中的稳定控制器标识。</summary>
        string ControllerId { get; }

        /// <summary>获取控制器真实物理通道数量，通道从 1 开始。</summary>
        int ChannelCount { get; }

        /// <summary>获取频闪脉宽闭区间；不支持频闪时为空。</summary>
        LightValueRange PulseWidthRange { get; }

        /// <summary>获取控制器当前可识别的工作模式；设备模式未知时为空。</summary>
        LightOperatingMode? CurrentMode { get; }

        /// <summary>判断控制器是否支持指定工作模式。</summary>
        bool Supports(LightOperatingMode mode);

        /// <summary>判断指定模式是否能够读取状态用于失败恢复。</summary>
        bool CanReadState(LightOperatingMode mode);

        /// <summary>判断指定模式是否支持显式关闭。</summary>
        bool CanTurnOff(LightOperatingMode mode);

        /// <summary>异步取得单控制器独占控制租约。</summary>
        Task<ILightControllerControlLease> AcquireAsync(
            CancellationToken cancellationToken);
    }

    /// <summary>持有一个物理光源控制器的独占访问权，并提供物理通道级控制能力。</summary>
    public interface ILightControllerControlLease
    {
        /// <summary>将当前控制器切换到目标工作模式。</summary>
        Task SwitchModeAsync(
            LightOperatingMode mode,
            CancellationToken cancellationToken);

        /// <summary>读取真实物理通道状态。</summary>
        Task<IReadOnlyList<LightChannelSetting>> ReadAsync(
            LightOperatingMode mode,
            IReadOnlyList<int> lightIndexes,
            CancellationToken cancellationToken);

        /// <summary>向真实物理通道应用常亮或频闪设定。</summary>
        Task ApplyAsync(
            LightOperatingMode mode,
            IReadOnlyList<LightChannelSetting> settings,
            CancellationToken cancellationToken);

        /// <summary>关闭指定真实物理通道；实际关闭粒度由设备能力决定。</summary>
        Task TurnOffAsync(
            IReadOnlyList<int> lightIndexes,
            CancellationToken cancellationToken);

        /// <summary>幂等释放独占访问权；释放后不得继续调用控制方法。</summary>
        Task ReleaseAsync();
    }
}
