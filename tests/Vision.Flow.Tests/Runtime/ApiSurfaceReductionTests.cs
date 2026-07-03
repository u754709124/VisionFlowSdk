using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Tests
{
    // 公共面收缩测试通过反射保护运行契约，避免重新引入非相机设备、保存和队列 API。
    internal static class ApiSurfaceReductionTests
    {
        public static Task NonCameraContractsAreNotExposed()
        {
            AssertTypeMissing(DeviceType("I", "Light", "Adapter"));
            AssertTypeMissing(DeviceType("I", "Motion", "Adapter"));
            AssertTypeMissing(DeviceType("I", "Recipe", "Adapter"));
            AssertTypeMissing(DeviceType("I", "ImageSave", "Adapter"));
            AssertTypeMissing(DeviceType("I", "Database", "Adapter"));
            AssertTypeMissing(DeviceType("Light", "ChannelSetting"));
            AssertTypeMissing(DeviceType("Motion", "Message"));
            AssertTypeMissing(DeviceType("Motion", "EventArgs"));
            AssertTypeMissing(DeviceType("Recipe", "RunRequest"));
            AssertTypeMissing(DeviceType("Recipe", "RunResult"));
            AssertTypeMissing(DeviceType("ImageSave", "Request"));
            AssertTypeMissing(DeviceType("ImageSave", "Result"));
            AssertTypeMissing(DeviceType("Database", "SaveRequest"));

            var methods = typeof(IDeviceRegistry)
                .GetMethods()
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            AssertEx.SequenceEqual(
                new[] { "GetCamera", "TryGetCamera" },
                methods,
                "IDeviceRegistry should only expose camera lookup methods.");
            return Task.FromResult(0);
        }

        public static Task QueueRuntimeIsNotExposed()
        {
            var serviceNamespace = RemovedServiceNamespace();
            AssertTypeMissing(serviceNamespace + ".I" + QueueType("Registry"));
            AssertTypeMissing(serviceNamespace + "." + QueueType(string.Empty));
            AssertTypeMissing(serviceNamespace + "." + QueueType("Options"));
            AssertTypeMissing(serviceNamespace + "." + QueueType("FullMode"));
            AssertTypeMissing(serviceNamespace + ".Flow" + RemovedName("Queue", "Names"));

            AssertEx.False(
                typeof(FlowExecutionContext).GetProperties().Any(x => string.Equals(x.Name, "Queues", StringComparison.Ordinal)),
                "FlowExecutionContext should not expose queue services.");
            AssertEx.False(
                typeof(FlowExecutionContext).GetConstructors().Any(HasQueueParameter),
                "FlowExecutionContext constructors should not accept queue services.");
            AssertEx.False(
                Enum.GetNames(typeof(FlowRuntimeEventType)).Any(x => x.StartsWith("Queue", StringComparison.Ordinal)),
                "Runtime event types should not expose queue events.");
            return Task.FromResult(0);
        }

        public static Task CameraFrameRouterSurfaceIsNotExposed()
        {
            AssertTypeMissing("Vision.Flow.Core.Runtime.CameraFrames.ICameraFrameRouter");
            AssertTypeMissing("Vision.Flow.Core.Runtime.CameraFrames.DefaultCameraFrameRouter");
            AssertTypeMissing("Vision.Flow.Core.Runtime.CameraFrames.CameraFrameWaitTicket");
            AssertTypeMissing("Vision.Flow.Core.Runtime.CameraFrames.CameraFrameStreamSubscription");
            AssertTypeMissing("Vision.Flow.Core.Runtime.CameraFrames.CameraFrameMatchMode");
            AssertTypeMissing("Vision.Flow.Core.Runtime.CameraFrames.CameraCallbackMode");
            AssertTypeMissing("Vision.Flow.Core.Runtime.CameraFrames.CameraStreamOutputMode");
            AssertEx.False(
                typeof(FlowToken).GetProperties().Any(x => string.Equals(x.Name, RemovedName("Capture", "GroupId"), StringComparison.Ordinal) || string.Equals(x.Name, RemovedName("Scan", "GroupId"), StringComparison.Ordinal)),
                "FlowToken should not expose capture or scan grouping fields.");
            return Task.FromResult(0);
        }

        public static Task DomainConstantsDoNotExposeRemovedNames()
        {
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Use", "Queue"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Queue", "Name"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Queue", "Capacity"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Queue", "MaxDegreeOfParallelism"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Queue", "FullMode"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Motion", "Id"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Light", "Id"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Recipe", "Id"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Database", "Id"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Saver", "Id"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("ImageSaver", "Id"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Scan", "GroupId"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Capture", "GroupId"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Frame", "IndexSource"));
            AssertNoPublicConstant(typeof(FlowSettingNames), RemovedName("Frame", "Index"));

            AssertNoPublicConstant(typeof(FlowOutputNames), "Queued");
            AssertNoPublicConstant(typeof(FlowOutputNames), RemovedName("Queue", "Completed"));
            AssertNoPublicConstant(typeof(FlowOutputNames), RemovedName("Image", "Path"));
            AssertNoPublicConstant(typeof(FlowOutputNames), "Saved");
            AssertNoPublicConstant(typeof(FlowOutputNames), RemovedName("Scan", "GroupId"));
            AssertNoPublicConstant(typeof(FlowOutputNames), RemovedName("Capture", "GroupId"));
            AssertNoPublicConstant(typeof(FlowOutputNames), RemovedName("Frame", "Index"));
            AssertNoPublicConstant(typeof(FlowOutputNames), RemovedName("Stitched", "Image"));
            AssertNoPublicConstant(typeof(FlowOutputNames), RemovedName("Final", "3DImage"));

            AssertNoPublicConstant(typeof(FlowMetadataKeys), RemovedName("Scan", "GroupId"));
            AssertNoPublicConstant(typeof(FlowMetadataKeys), RemovedName("Capture", "GroupId"));
            AssertNoPublicConstant(typeof(FlowMetadataKeys), RemovedName("Shot", "Index"));
            AssertNoPublicConstant(typeof(FlowMetadataKeys), RemovedName("Frame", "Index"));
            AssertNoPublicConstant(typeof(FlowMetadataKeys), RemovedName("Saver", "Id"));

            AssertNoPublicConstant(typeof(FlowNodeTypePrefixes), "Light");
            AssertNoPublicConstant(typeof(FlowNodeTypePrefixes), "Database");
            AssertNoPublicConstant(typeof(FlowNodeTypePrefixes), "Group");
            AssertNoPublicConstant(typeof(FlowNodeTypePrefixes), "Scan");
            AssertNoPublicConstant(typeof(FlowNodeTypePrefixes), "Fusion");
            return Task.FromResult(0);
        }

        private static string DeviceType(params string[] parts)
        {
            return "Vision.Flow.Core.Contracts.Devices." + string.Concat(parts);
        }

        private static string RemovedServiceNamespace()
        {
            return "Vision.Flow.Core.Runtime." + "Queues";
        }

        private static string QueueType(string suffix)
        {
            return "Flow" + RemovedName("Task", "Queue") + suffix;
        }

        private static string RemovedName(params string[] parts)
        {
            return string.Concat(parts);
        }

        private static void AssertTypeMissing(string typeName)
        {
            var type = typeof(ICameraAdapter).Assembly.GetType(typeName, false);
            AssertEx.True(type == null, "Type should not be exposed: " + typeName);
        }

        private static void AssertNoPublicConstant(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            AssertEx.True(field == null, type.Name + " should not expose constant: " + name);
        }

        private static bool HasQueueParameter(ConstructorInfo constructor)
        {
            return constructor.GetParameters().Any(x => x.ParameterType.FullName != null &&
                x.ParameterType.FullName.IndexOf(RemovedServiceNamespace(), StringComparison.Ordinal) >= 0);
        }
    }
}
