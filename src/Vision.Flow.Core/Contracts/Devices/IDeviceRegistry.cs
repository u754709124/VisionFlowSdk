namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 设备适配器注册表，运行时节点只能通过该接口获取相机适配器。
    /// </summary>
    public interface IDeviceRegistry
    {
        bool TryGetCamera(string cameraId, out ICameraAdapter camera);

        ICameraAdapter GetCamera(string cameraId);
    }
}
