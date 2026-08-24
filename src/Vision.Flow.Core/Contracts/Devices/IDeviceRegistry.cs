namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 设备适配器注册表，运行时节点通过稳定标识和 Adapter 契约获取设备能力。
    /// </summary>
    public interface IDeviceRegistry
    {
        /// <summary>尝试按稳定标识获取相机 Adapter。</summary>
        bool TryGetCamera(string cameraId, out ICameraAdapter camera);

        /// <summary>按稳定标识获取相机 Adapter。</summary>
        ICameraAdapter GetCamera(string cameraId);

        /// <summary>尝试按稳定标识获取单光源控制器 Adapter。</summary>
        bool TryGetLightController(
            string controllerId,
            out ILightControllerAdapter controller);

        /// <summary>按稳定标识获取单光源控制器 Adapter。</summary>
        ILightControllerAdapter GetLightController(string controllerId);

        /// <summary>尝试按稳定标识获取运控 Adapter。</summary>
        bool TryGetMotion(string motionId, out IMotionAdapter motion);

        /// <summary>按稳定标识获取运控 Adapter。</summary>
        IMotionAdapter GetMotion(string motionId);
    }
}
