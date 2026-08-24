namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>定义光源控制器的稳定工作模式协议。</summary>
    public enum LightOperatingMode
    {
        /// <summary>常亮模式。</summary>
        Continuous = 0,

        /// <summary>外部触发频闪模式。</summary>
        Strobe = 1
    }

    /// <summary>表示包含上下界的不可变整数范围。</summary>
    public sealed class LightValueRange
    {
        /// <summary>创建包含上下界的范围。</summary>
        public LightValueRange(int minimum, int maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        /// <summary>获取范围下界。</summary>
        public int Minimum { get; }

        /// <summary>获取范围上界。</summary>
        public int Maximum { get; }

        /// <summary>判断值是否位于闭区间内。</summary>
        public bool Contains(int value)
        {
            return value >= Minimum && value <= Maximum;
        }
    }

    /// <summary>表示与具体光源 SDK 无关的不可变通道设置。</summary>
    public sealed class LightChannelSetting
    {
        /// <summary>初始化通道、亮度及脉宽设置。</summary>
        public LightChannelSetting(
            int channelIndex,
            int brightness,
            int durationMicroseconds)
        {
            ChannelIndex = channelIndex;
            Brightness = brightness;
            DurationMicroseconds = durationMicroseconds;
        }

        /// <summary>获取逻辑或物理通道编号，具体语义由调用边界决定。</summary>
        public int ChannelIndex { get; }

        /// <summary>获取目标亮度。</summary>
        public int Brightness { get; }

        /// <summary>获取脉宽微秒数。</summary>
        public int DurationMicroseconds { get; }
    }
}
