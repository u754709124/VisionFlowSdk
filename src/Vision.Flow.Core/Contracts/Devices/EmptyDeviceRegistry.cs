using System.Collections.Generic;

namespace Vision.Flow.Core.Contracts.Devices
{
    internal sealed class EmptyDeviceRegistry : IDeviceRegistry
    {
        public static readonly EmptyDeviceRegistry Instance = new EmptyDeviceRegistry();

        private EmptyDeviceRegistry()
        {
        }

        public bool TryGetCamera(string cameraId, out ICameraAdapter camera)
        {
            camera = null;
            return false;
        }

        public ICameraAdapter GetCamera(string cameraId)
        {
            throw CreateMissingDeviceException("camera", cameraId);
        }

        private static KeyNotFoundException CreateMissingDeviceException(string deviceType, string deviceId)
        {
            return new KeyNotFoundException("Device registry does not contain " + deviceType + ": " + deviceId);
        }
    }
}
