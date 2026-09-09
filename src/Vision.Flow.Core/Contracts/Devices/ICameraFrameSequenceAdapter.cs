namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 为相机 Adapter 提供采集帧序号复位能力；实现应保证复位与帧序号分配互斥。
    /// </summary>
    public interface ICameraFrameSequenceAdapter
    {
        /// <summary>
        /// 将当前采集帧序号复位为零，并返回复位前最后分配的序号。
        /// </summary>
        /// <returns>复位前最后分配的采集帧序号；尚未分配时返回零。</returns>
        int ResetCaptureFrameSequence();
    }
}
