using System;
using System.Collections.Generic;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    public sealed class DefaultDeviceRegistry : IDeviceRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, ICameraAdapter> _cameras;
        private readonly Dictionary<string, ILightAdapter> _lights;
        private readonly Dictionary<string, IMotionAdapter> _motions;
        private readonly Dictionary<string, IRecipeAdapter> _recipes;
        private readonly Dictionary<string, IImageSaveAdapter> _imageSavers;
        private readonly Dictionary<string, IDatabaseAdapter> _databases;

        public DefaultDeviceRegistry()
        {
            _cameras = new Dictionary<string, ICameraAdapter>(StringComparer.OrdinalIgnoreCase);
            _lights = new Dictionary<string, ILightAdapter>(StringComparer.OrdinalIgnoreCase);
            _motions = new Dictionary<string, IMotionAdapter>(StringComparer.OrdinalIgnoreCase);
            _recipes = new Dictionary<string, IRecipeAdapter>(StringComparer.OrdinalIgnoreCase);
            _imageSavers = new Dictionary<string, IImageSaveAdapter>(StringComparer.OrdinalIgnoreCase);
            _databases = new Dictionary<string, IDatabaseAdapter>(StringComparer.OrdinalIgnoreCase);
        }

        public DefaultDeviceRegistry RegisterCamera(ICameraAdapter camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException("camera");
            }

            return RegisterCamera(camera.CameraId, camera);
        }

        public DefaultDeviceRegistry RegisterCamera(string cameraId, ICameraAdapter camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException("camera");
            }

            RegisterDevice(_cameras, cameraId, camera, "cameraId");
            return this;
        }

        public DefaultDeviceRegistry RegisterLight(ILightAdapter light)
        {
            if (light == null)
            {
                throw new ArgumentNullException("light");
            }

            return RegisterLight(light.LightId, light);
        }

        public DefaultDeviceRegistry RegisterLight(string lightId, ILightAdapter light)
        {
            if (light == null)
            {
                throw new ArgumentNullException("light");
            }

            RegisterDevice(_lights, lightId, light, "lightId");
            return this;
        }

        public DefaultDeviceRegistry RegisterMotion(IMotionAdapter motion)
        {
            if (motion == null)
            {
                throw new ArgumentNullException("motion");
            }

            return RegisterMotion(motion.MotionId, motion);
        }

        public DefaultDeviceRegistry RegisterMotion(string motionId, IMotionAdapter motion)
        {
            if (motion == null)
            {
                throw new ArgumentNullException("motion");
            }

            RegisterDevice(_motions, motionId, motion, "motionId");
            return this;
        }

        public DefaultDeviceRegistry RegisterRecipe(IRecipeAdapter recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException("recipe");
            }

            return RegisterRecipe(recipe.RecipeId, recipe);
        }

        public DefaultDeviceRegistry RegisterRecipe(string recipeId, IRecipeAdapter recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException("recipe");
            }

            RegisterDevice(_recipes, recipeId, recipe, "recipeId");
            return this;
        }

        public DefaultDeviceRegistry RegisterImageSaver(IImageSaveAdapter imageSaver)
        {
            if (imageSaver == null)
            {
                throw new ArgumentNullException("imageSaver");
            }

            return RegisterImageSaver(imageSaver.SaverId, imageSaver);
        }

        public DefaultDeviceRegistry RegisterImageSaver(string saverId, IImageSaveAdapter imageSaver)
        {
            if (imageSaver == null)
            {
                throw new ArgumentNullException("imageSaver");
            }

            RegisterDevice(_imageSavers, saverId, imageSaver, "saverId");
            return this;
        }

        public DefaultDeviceRegistry RegisterDatabase(IDatabaseAdapter database)
        {
            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            return RegisterDatabase(database.DatabaseId, database);
        }

        public DefaultDeviceRegistry RegisterDatabase(string databaseId, IDatabaseAdapter database)
        {
            if (database == null)
            {
                throw new ArgumentNullException("database");
            }

            RegisterDevice(_databases, databaseId, database, "databaseId");
            return this;
        }

        public bool TryGetCamera(string cameraId, out ICameraAdapter camera)
        {
            return TryGetDevice(_cameras, cameraId, out camera);
        }

        public ICameraAdapter GetCamera(string cameraId)
        {
            return GetDevice(_cameras, cameraId, "camera", "cameraId");
        }

        public bool TryGetLight(string lightId, out ILightAdapter light)
        {
            return TryGetDevice(_lights, lightId, out light);
        }

        public ILightAdapter GetLight(string lightId)
        {
            return GetDevice(_lights, lightId, "light", "lightId");
        }

        public bool TryGetMotion(string motionId, out IMotionAdapter motion)
        {
            return TryGetDevice(_motions, motionId, out motion);
        }

        public IMotionAdapter GetMotion(string motionId)
        {
            return GetDevice(_motions, motionId, "motion", "motionId");
        }

        public bool TryGetRecipe(string recipeId, out IRecipeAdapter recipe)
        {
            return TryGetDevice(_recipes, recipeId, out recipe);
        }

        public IRecipeAdapter GetRecipe(string recipeId)
        {
            return GetDevice(_recipes, recipeId, "recipe", "recipeId");
        }

        public bool TryGetImageSaver(string saverId, out IImageSaveAdapter imageSaver)
        {
            return TryGetDevice(_imageSavers, saverId, out imageSaver);
        }

        public IImageSaveAdapter GetImageSaver(string saverId)
        {
            return GetDevice(_imageSavers, saverId, "image saver", "saverId");
        }

        public bool TryGetDatabase(string databaseId, out IDatabaseAdapter database)
        {
            return TryGetDevice(_databases, databaseId, out database);
        }

        public IDatabaseAdapter GetDatabase(string databaseId)
        {
            return GetDevice(_databases, databaseId, "database", "databaseId");
        }

        private void RegisterDevice<TDevice>(
            Dictionary<string, TDevice> devices,
            string deviceId,
            TDevice device,
            string parameterName)
            where TDevice : class
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device id is required.", parameterName);
            }

            lock (_gate)
            {
                devices[deviceId] = device;
            }
        }

        private bool TryGetDevice<TDevice>(
            Dictionary<string, TDevice> devices,
            string deviceId,
            out TDevice device)
            where TDevice : class
        {
            device = null;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return false;
            }

            lock (_gate)
            {
                return devices.TryGetValue(deviceId, out device);
            }
        }

        private TDevice GetDevice<TDevice>(
            Dictionary<string, TDevice> devices,
            string deviceId,
            string deviceType,
            string parameterName)
            where TDevice : class
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device id is required.", parameterName);
            }

            TDevice device;
            lock (_gate)
            {
                if (devices.TryGetValue(deviceId, out device))
                {
                    return device;
                }
            }

            throw new KeyNotFoundException("Device registry does not contain " + deviceType + ": " + deviceId);
        }
    }
}
