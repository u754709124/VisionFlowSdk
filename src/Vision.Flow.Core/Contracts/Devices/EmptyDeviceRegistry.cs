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

        public bool TryGetLight(string lightId, out ILightAdapter light)
        {
            light = null;
            return false;
        }

        public ILightAdapter GetLight(string lightId)
        {
            throw CreateMissingDeviceException("light", lightId);
        }

        public bool TryGetMotion(string motionId, out IMotionAdapter motion)
        {
            motion = null;
            return false;
        }

        public IMotionAdapter GetMotion(string motionId)
        {
            throw CreateMissingDeviceException("motion", motionId);
        }

        public bool TryGetRecipe(string recipeId, out IRecipeAdapter recipe)
        {
            recipe = null;
            return false;
        }

        public IRecipeAdapter GetRecipe(string recipeId)
        {
            throw CreateMissingDeviceException("recipe", recipeId);
        }

        public bool TryGetImageSaver(string saverId, out IImageSaveAdapter imageSaver)
        {
            imageSaver = null;
            return false;
        }

        public IImageSaveAdapter GetImageSaver(string saverId)
        {
            throw CreateMissingDeviceException("image saver", saverId);
        }

        public bool TryGetDatabase(string databaseId, out IDatabaseAdapter database)
        {
            database = null;
            return false;
        }

        public IDatabaseAdapter GetDatabase(string databaseId)
        {
            throw CreateMissingDeviceException("database", databaseId);
        }

        private static KeyNotFoundException CreateMissingDeviceException(string deviceType, string deviceId)
        {
            return new KeyNotFoundException("Device registry does not contain " + deviceType + ": " + deviceId);
        }
    }
}
