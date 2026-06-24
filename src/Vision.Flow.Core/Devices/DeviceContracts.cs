using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core
{
    public interface IDeviceRegistry
    {
        bool TryGetCamera(string cameraId, out ICameraAdapter camera);

        ICameraAdapter GetCamera(string cameraId);

        bool TryGetLight(string lightId, out ILightAdapter light);

        ILightAdapter GetLight(string lightId);

        bool TryGetMotion(string motionId, out IMotionAdapter motion);

        IMotionAdapter GetMotion(string motionId);

        bool TryGetRecipe(string recipeId, out IRecipeAdapter recipe);

        IRecipeAdapter GetRecipe(string recipeId);

        bool TryGetImageSaver(string saverId, out IImageSaveAdapter imageSaver);

        IImageSaveAdapter GetImageSaver(string saverId);

        bool TryGetDatabase(string databaseId, out IDatabaseAdapter database);

        IDatabaseAdapter GetDatabase(string databaseId);
    }

    public interface ICameraAdapter
    {
        string CameraId { get; }

        IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors();

        Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken);

        Task<object> GetParameterAsync(string parameterName, CancellationToken cancellationToken);

        Task SoftTriggerAsync(CameraTriggerContext triggerContext, CancellationToken cancellationToken);

        event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;
    }

    public interface ILightAdapter
    {
        string LightId { get; }

        Task SetAsync(LightChannelSetting setting, CancellationToken cancellationToken);

        Task TurnOffAsync(string channelName, CancellationToken cancellationToken);
    }

    public interface IMotionAdapter
    {
        string MotionId { get; }

        Task MoveToAsync(string positionName, CancellationToken cancellationToken);

        Task WaitForInPositionAsync(string positionName, CancellationToken cancellationToken);
    }

    public interface IRecipeAdapter
    {
        string RecipeId { get; }

        Task<RecipeRunResult> RunAsync(RecipeRunRequest request, CancellationToken cancellationToken);
    }

    public interface IImageSaveAdapter
    {
        string SaverId { get; }

        Task<ImageSaveResult> SaveAsync(ImageSaveRequest request, CancellationToken cancellationToken);
    }

    public interface IDatabaseAdapter
    {
        string DatabaseId { get; }

        Task SaveAsync(DatabaseSaveRequest request, CancellationToken cancellationToken);
    }

    public interface IVisionImage
    {
        string ImageId { get; }

        int Width { get; }

        int Height { get; }

        string PixelFormat { get; }

        DateTime CreatedUtc { get; }

        byte[] Data { get; }

        IDictionary<string, object> Metadata { get; }
    }

    public sealed class CameraTriggerContext
    {
        public CameraTriggerContext()
        {
            TriggerId = Guid.NewGuid().ToString("N");
            Metadata = new Dictionary<string, object>();
        }

        public string CameraId { get; set; }

        public string TriggerId { get; set; }

        public FlowToken Token { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class CameraFrameArrivedEventArgs : EventArgs
    {
        public CameraFrameArrivedEventArgs(CameraFrameData frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            Frame = frame;
        }

        public CameraFrameData Frame { get; private set; }
    }

    public sealed class CameraFrameData
    {
        public CameraFrameData()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string CameraId { get; set; }

        public string TriggerId { get; set; }

        public string FrameId { get; set; }

        public DateTime GrabTime { get; set; }

        public IVisionImage Image { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class CameraParameterDescriptor
    {
        public string ParameterName { get; set; }

        public string DisplayName { get; set; }

        public string ValueType { get; set; }

        public string Unit { get; set; }

        public bool IsWritable { get; set; }

        public object Minimum { get; set; }

        public object Maximum { get; set; }

        public object DefaultValue { get; set; }
    }

    public sealed class LightChannelSetting
    {
        public string LightId { get; set; }

        public string ChannelName { get; set; }

        public bool IsEnabled { get; set; }

        public double Intensity { get; set; }

        public int DurationMs { get; set; }
    }

    public sealed class RecipeRunRequest
    {
        public RecipeRunRequest()
        {
            Inputs = new Dictionary<string, object>();
        }

        public string RecipeId { get; set; }

        public FlowToken Token { get; set; }

        public IDictionary<string, object> Inputs { get; set; }
    }

    public sealed class RecipeRunResult
    {
        public RecipeRunResult()
        {
            Outputs = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public IDictionary<string, object> Outputs { get; set; }
    }

    public sealed class ImageSaveRequest
    {
        public ImageSaveRequest()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string SaverId { get; set; }

        public IVisionImage Image { get; set; }

        public string DirectoryPath { get; set; }

        public string FileName { get; set; }

        public string Format { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class ImageSaveResult
    {
        public ImageSaveResult()
        {
            Metadata = new Dictionary<string, object>();
        }

        public bool IsSuccess { get; set; }

        public string Path { get; set; }

        public string Message { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    public sealed class DatabaseSaveRequest
    {
        public DatabaseSaveRequest()
        {
            Values = new Dictionary<string, object>();
            Metadata = new Dictionary<string, object>();
        }

        public string DatabaseId { get; set; }

        public string TableName { get; set; }

        public IDictionary<string, object> Values { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

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
