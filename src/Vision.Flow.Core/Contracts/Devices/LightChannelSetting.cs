namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 光源通道设置，表达单个通道的开关、亮度和持续时间。
    /// </summary>
    public sealed class LightChannelSetting
    {
        public string LightId { get; set; }

        public string ChannelName { get; set; }

        public bool IsEnabled { get; set; }

        public double Intensity { get; set; }

        public int DurationMs { get; set; }
    }
}
