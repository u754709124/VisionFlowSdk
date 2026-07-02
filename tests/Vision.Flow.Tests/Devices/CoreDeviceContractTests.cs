using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // Core 设备契约测试只使用本地最小桩，避免 SDK 测试依赖 Fake Adapter 项目。
    internal static class CoreDeviceContractTests
    {
        public static async Task CameraFrameRouterRoutesLocalTestCamera()
        {
            using (var router = new DefaultCameraFrameRouter())
            {
                var camera = new TestCameraAdapter("Camera01");
                var waitTask = router.WaitForFrameAsync(
                    camera,
                    new CameraFrameWaitTicket
                    {
                        CameraId = "Camera01",
                        MatchMode = CameraFrameMatchModes.TriggerId,
                        TriggerId = "trigger-001"
                    },
                    1000,
                    CancellationToken.None);

                camera.EmitFrame("trigger-001", "frame-001");
                var frame = await waitTask.ConfigureAwait(false);

                AssertEx.NotNull(frame, "Frame router should return the matching frame.");
                AssertEx.Equal("Camera01", frame.CameraId, "Frame camera id should match.");
                AssertEx.Equal("trigger-001", frame.TriggerId, "Frame trigger id should match.");
                AssertEx.Equal("frame-001", frame.FrameId, "Frame id should match.");
            }
        }

        public static Task VisionImageReferenceLifecycle()
        {
            var native = new DisposableProbe();
            var image = new VisionImageReference("image-001", 5, 6, "Mono8", new byte[] { 1, 2 }, native, true, "Raw");
            image.Metadata[FlowMetadataKeys.FrameId] = "frame-001";

            var clone = image.CloneReference();
            image.Dispose();

            byte[] bytes;
            AssertEx.True(native.IsDisposed, "Owned native image should be disposed.");
            AssertEx.False(image.TryGetBytes(out bytes), "Disposed image should not expose bytes.");
            AssertEx.True(clone.TryGetBytes(out bytes), "Cloned image reference should keep byte data.");
            AssertEx.Equal(2, bytes.Length, "Cloned bytes length should match.");
            AssertEx.Equal("frame-001", Convert.ToString(clone.Metadata[FlowMetadataKeys.FrameId]), "Clone should copy metadata.");
            clone.Dispose();
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

            public Task SoftTriggerAsync(CameraTriggerContext triggerContext, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EmitFrame(triggerContext == null ? null : triggerContext.TriggerId, Guid.NewGuid().ToString("N"));
                return Task.FromResult(0);
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
                            FrameId = frameId,
                            GrabTime = DateTime.UtcNow,
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
