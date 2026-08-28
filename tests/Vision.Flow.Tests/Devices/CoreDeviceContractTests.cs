using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // Core 设备契约测试只使用本地最小桩，避免 SDK 测试依赖 Fake Adapter 项目。
    internal static class CoreDeviceContractTests
    {
        public static Task VisionImageReferenceLifecycle()
        {
            var native = new DisposableProbe();
            var image = new VisionImageReference("image-001", 5, 6, "Mono8", new byte[] { 1, 2 }, native, true, "Raw");
            AssertEx.Equal(DateTimeKind.Local, image.CreatedAt.Kind, "Vision images must use the running machine's local creation time.");
            image.Metadata[FlowMetadataKeys.CaptureFrameId] = "frame-001";

            var clone = image.CloneReference();
            image.Dispose();

            byte[] bytes;
            AssertEx.True(native.IsDisposed, "Owned native image should be disposed.");
            AssertEx.False(image.TryGetBytes(out bytes), "Disposed image should not expose bytes.");
            AssertEx.True(clone.TryGetBytes(out bytes), "Cloned image reference should keep byte data.");
            AssertEx.Equal(2, bytes.Length, "Cloned bytes length should match.");
            AssertEx.Equal("frame-001", Convert.ToString(clone.Metadata[FlowMetadataKeys.CaptureFrameId]), "Clone should copy metadata.");
            clone.Dispose();
            return Task.FromResult(0);
        }

        public static Task MotionAdapterModelsUseReadOnlySnapshots()
        {
            var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "Position", 12.5 }
            };
            var request = new MotionAdapterCommandRequest(
                "MoveAbsolute",
                parameters,
                TimeSpan.FromSeconds(2));
            parameters["Position"] = 99.0;

            AssertEx.Equal(12.5, Convert.ToDouble(request.Parameters["Position"]), "Request should copy parameters.");
            AssertEx.Equal(TimeSpan.FromSeconds(2), request.ResponseTimeout.Value, "Request should keep response timeout.");

            var result = new MotionAdapterCommandResult(
                request.CommandName,
                "sent",
                "received",
                new Dictionary<string, object> { { "Accepted", true } });
            var received = new MotionAdapterCommandReceivedEventArgs(
                "Motion-A",
                "PositionChanged",
                "P01",
                "P01,12.5",
                new Dictionary<string, object> { { "Position", 12.5 } });

            AssertEx.Equal("MoveAbsolute", result.CommandName, "Result should keep logical command name.");
            AssertEx.Equal("PositionChanged", received.CommandName, "Event should keep logical command name.");
            AssertEx.Equal("P01", received.WireCode, "Event should keep wire command code as diagnostic data.");
            return Task.FromResult(0);
        }

        public static Task LightControllerRegistryUsesExplicitContract()
        {
            MethodInfo getLight = typeof(IDeviceRegistry).GetMethod(
                "GetLightController",
                new[] { typeof(string) });
            MethodInfo tryGetLight = typeof(IDeviceRegistry).GetMethod(
                "TryGetLightController");

            AssertEx.True(getLight != null, "Registry should expose explicit light lookup.");
            AssertEx.Equal(
                typeof(ILightControllerAdapter),
                getLight.ReturnType,
                "Light lookup should return the Core light Adapter contract.");
            AssertEx.True(
                tryGetLight != null &&
                tryGetLight.GetParameters().Last().ParameterType ==
                    typeof(ILightControllerAdapter).MakeByRefType(),
                "TryGet light lookup should use an explicit out Adapter contract.");
            AssertEx.False(
                typeof(IDeviceRegistry).GetMethods().Any(x => x.IsGenericMethodDefinition),
                "Device registry should not require generic Adapter lookup.");

            var range = new LightValueRange(10, 20);
            var setting = new LightChannelSetting(2, 128, 15);
            AssertEx.True(range.Contains(10) && range.Contains(20), "Light range should include both bounds.");
            AssertEx.Equal(2, setting.ChannelIndex, "Light setting should keep its physical channel.");
            AssertEx.Equal(128, setting.Brightness, "Light setting should keep brightness.");
            return Task.FromResult(0);
        }

        private sealed class TestCameraAdapter : ICameraAdapter
        {
            private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public TestCameraAdapter(string cameraId)
            {
                CameraId = cameraId;
            }

            public event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;

            public string CameraId { get; private set; }

            public IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors()
            {
                return new CameraParameterDescriptor[0];
            }

            public Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _parameters[parameterName] = value;
                return Task.FromResult(0);
            }

            public Task<object> GetParameterAsync(string parameterName, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object value;
                _parameters.TryGetValue(parameterName, out value);
                return Task.FromResult(value);
            }

            public Task<CameraFrameData> GrabOneAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CameraFrameData
                {
                    CameraId = CameraId,
                    TriggerId = "grab-one",
                    CaptureFrameId = Guid.NewGuid().ToString("N"),
                    GrabTime = DateTime.Now,
                    Image = new VisionImageReference("grab-one-image", 1, 1, "Mono8", new byte[] { 7 })
                });
            }

            public void EmitFrame(string triggerId, string frameId)
            {
                var handler = FrameArrived;
                if (handler == null)
                {
                    return;
                }

                handler(
                    this,
                    new CameraFrameArrivedEventArgs(
                        new CameraFrameData
                        {
                            CameraId = CameraId,
                            TriggerId = triggerId,
                            CaptureFrameId = frameId,
                            GrabTime = DateTime.Now,
                            Image = new VisionImageReference(frameId, 1, 1, "Mono8", new byte[] { 7 })
                        }));
            }
        }

        private sealed class DisposableProbe : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
