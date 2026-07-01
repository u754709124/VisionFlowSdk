using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision.Flow.Core
{
    /// <summary>
    /// 设备适配器注册表，运行时节点只能通过该接口获取相机、光源、运控、算法和存储适配器。
    /// </summary>
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

    /// <summary>
    /// 相机适配器接口，封装真实相机 SDK 或 Fake 相机的参数、软触发和帧回调能力。
    /// </summary>
    public interface ICameraAdapter
    {
        string CameraId { get; }

        IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors();

        Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken);

        Task<object> GetParameterAsync(string parameterName, CancellationToken cancellationToken);

        Task SoftTriggerAsync(CameraTriggerContext triggerContext, CancellationToken cancellationToken);

        event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;
    }

    /// <summary>
    /// 光源适配器接口，节点通过它设置通道亮度和关闭光源。
    /// </summary>
    public interface ILightAdapter
    {
        string LightId { get; }

        Task SetAsync(LightChannelSetting setting, CancellationToken cancellationToken);

        Task TurnOffAsync(string channelName, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 运控适配器接口，封装到位、等待和运动消息通知能力。
    /// </summary>
    public interface IMotionAdapter
    {
        string MotionId { get; }

        Task MoveToAsync(string positionName, CancellationToken cancellationToken);

        Task WaitForInPositionAsync(string positionName, CancellationToken cancellationToken);

        Task SendMessageAsync(MotionMessage message, CancellationToken cancellationToken);

        event EventHandler<MotionEventArgs> MotionEventReceived;
    }

    /// <summary>
    /// 算法配方适配器接口，供项目专属节点调用上位机或测试桩中的算法实现。
    /// </summary>
    public interface IRecipeAdapter
    {
        string RecipeId { get; }

        Task<RecipeRunResult> RunAsync(RecipeRunRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 图像保存适配器接口，避免节点直接依赖具体文件系统或业务存储逻辑。
    /// </summary>
    public interface IImageSaveAdapter
    {
        string SaverId { get; }

        Task<ImageSaveResult> SaveAsync(ImageSaveRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 数据库保存适配器接口，生产上位机可在外部实现真实数据入库逻辑。
    /// </summary>
    public interface IDatabaseAdapter
    {
        string DatabaseId { get; }

        Task SaveAsync(DatabaseSaveRequest request, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 视觉图像抽象，允许 Fake 图像、SDK 原生图像引用和后处理结果以统一方式流转。
    /// </summary>
    public interface IVisionImage : IDisposable
    {
        string ImageId { get; }

        int Width { get; }

        int Height { get; }

        string PixelFormat { get; }

        string ImageKind { get; }

        DateTime CreatedUtc { get; }

        byte[] Data { get; }

        object NativeImage { get; }

        bool IsDisposed { get; }

        IDictionary<string, object> Metadata { get; }

        IVisionImage CloneReference();

        bool TryGetBytes(out byte[] data);
    }

    /// <summary>
    /// 相机软触发上下文，用于把 TriggerId、Token 和业务元数据传给相机适配器。
    /// </summary>
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

    /// <summary>
    /// 相机帧到达事件参数，快速封装回调帧后交给运行时路由。
    /// </summary>
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

    /// <summary>
    /// 相机帧数据模型，承载图像、帧号、采集时间和匹配用元数据。
    /// </summary>
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

    /// <summary>
    /// 相机参数描述，用于设计器或上位机呈现可配置参数。
    /// </summary>
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

    /// <summary>
    /// 运控消息模型，用于运控与流程之间传递点位、采集组和扫描组上下文。
    /// </summary>
    public sealed class MotionMessage
    {
        public MotionMessage()
        {
            Metadata = new Dictionary<string, object>();
        }

        public string MessageType { get; set; }

        public string MotionId { get; set; }

        public string PositionId { get; set; }

        public string CaptureGroupId { get; set; }

        public string ScanGroupId { get; set; }

        public string TokenId { get; set; }

        public object Result { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// 运控事件参数，承载外部运控事件到流程触发所需的上下文。
    /// </summary>
    public sealed class MotionEventArgs : EventArgs
    {
        public MotionEventArgs()
        {
            TimestampUtc = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
        }

        public string MotionId { get; set; }

        public string EventType { get; set; }

        public string PositionId { get; set; }

        public string CaptureGroupId { get; set; }

        public string ScanGroupId { get; set; }

        public DateTime TimestampUtc { get; set; }

        public IDictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// 配方运行请求，节点把 Token、图像和业务输入整理后交给算法适配器。
    /// </summary>
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

    /// <summary>
    /// 配方运行结果，Outputs 字典会被节点转写为下游可绑定变量。
    /// </summary>
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

    /// <summary>
    /// 图像保存请求，描述待保存图像、目标路径和保存相关元数据。
    /// </summary>
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

    /// <summary>
    /// 图像保存结果，向流程返回保存状态、路径和附加元数据。
    /// </summary>
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

    /// <summary>
    /// 数据库保存请求，保存字段由公共节点根据变量绑定或映射配置组装。
    /// </summary>
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
