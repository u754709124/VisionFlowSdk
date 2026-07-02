namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// ��Դͨ�����ã���ﵥ��ͨ���Ŀ��ء����Ⱥͳ���ʱ�䡣
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
