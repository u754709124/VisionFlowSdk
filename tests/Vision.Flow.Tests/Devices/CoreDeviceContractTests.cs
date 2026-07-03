using System;
using System.Collections.Generic;
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
    // Core 璁惧濂戠害娴嬭瘯鍙娇鐢ㄦ湰鍦版渶灏忔々锛岄伩鍏?SDK 娴嬭瘯渚濊禆 Fake Adapter 椤圭洰銆?
    internal static class CoreDeviceContractTests
    {
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

            public Task<CameraFrameData> GrabOneAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new CameraFrameData
                {
                    CameraId = CameraId,
                    TriggerId = "grab-one",
                    FrameId = Guid.NewGuid().ToString("N"),
                    GrabTime = DateTime.UtcNow,
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
