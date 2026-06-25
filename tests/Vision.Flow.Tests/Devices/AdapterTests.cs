using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // 模拟适配器测试验证运行时和节点测试使用的设备侧契约。
    internal static class AdapterTests
    {
        public static Task RegistryGetsFakeCamera()
        {
            var registry = new DefaultDeviceRegistry();
            var camera = new FakeCameraAdapter("Camera01");

            registry.RegisterCamera(camera);

            ICameraAdapter resolvedByTryGet;
            AssertEx.True(registry.TryGetCamera("Camera01", out resolvedByTryGet), "Registry should find the registered fake camera.");
            AssertEx.True(object.ReferenceEquals(camera, resolvedByTryGet), "TryGetCamera should return the registered camera instance.");
            AssertEx.True(object.ReferenceEquals(camera, registry.GetCamera("Camera01")), "GetCamera should return the registered camera instance.");
            return Task.FromResult(0);
        }

        public static async Task SoftTriggerReceivesFrame()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var frameSource = new TaskCompletionSource<CameraFrameData>();

            camera.FrameArrived += delegate(object sender, CameraFrameArrivedEventArgs args)
            {
                frameSource.TrySetResult(args.Frame);
            };

            await camera.SoftTriggerAsync(
                new CameraTriggerContext
                {
                    CameraId = "Camera01",
                    TriggerId = "trigger-001"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.True(frameSource.Task.IsCompleted, "Default fake soft trigger should complete after FrameArrived is raised.");
            var completed = await Task.WhenAny(frameSource.Task, Task.Delay(1000)).ConfigureAwait(false);
            AssertEx.True(object.ReferenceEquals(frameSource.Task, completed), "Soft trigger should raise FrameArrived within the timeout.");

            var frame = await frameSource.Task.ConfigureAwait(false);
            AssertEx.NotNull(frame, "FrameArrived should provide frame data.");
            AssertEx.NotNull(frame.Image, "Frame data should include a fake image.");
            AssertEx.Equal("Camera01", frame.CameraId, "Frame camera id should match the adapter.");
            AssertEx.Equal("trigger-001", frame.TriggerId, "Frame trigger id should match the trigger context.");
            AssertEx.Equal("Camera01", Convert.ToString(frame.Metadata[FlowMetadataKeys.CameraId]), "Frame metadata should include CameraId.");
            AssertEx.Equal("trigger-001", Convert.ToString(frame.Metadata[FlowMetadataKeys.TriggerId]), "Frame metadata should include TriggerId.");
            AssertEx.True(frame.Metadata.ContainsKey("FrameId"), "Frame metadata should include FrameId.");
            AssertEx.True(frame.Metadata.ContainsKey("GrabTime"), "Frame metadata should include GrabTime.");
        }

        public static async Task SoftTriggerCancellationPreventsFrame()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 200
            };
            var frameRaised = false;
            camera.FrameArrived += delegate
            {
                frameRaised = true;
            };

            using (var cancellation = new CancellationTokenSource())
            {
                var triggerTask = camera.SoftTriggerAsync(
                    new CameraTriggerContext
                    {
                        CameraId = "Camera01",
                        TriggerId = "trigger-cancel"
                    },
                    cancellation.Token);
                cancellation.CancelAfter(10);

                await AssertEx.ThrowsAsync<OperationCanceledException>(
                    async delegate
                    {
                        await triggerTask.ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }

            await Task.Delay(250).ConfigureAwait(false);
            AssertEx.False(frameRaised, "Canceled fake soft trigger must not create a frame.");
        }

        public static async Task SoftTriggerCanReturnBeforeFrameArrived()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 50,
                ReturnBeforeFrameArrived = true
            };
            var frameSource = new TaskCompletionSource<CameraFrameData>();

            camera.FrameArrived += delegate(object sender, CameraFrameArrivedEventArgs args)
            {
                frameSource.TrySetResult(args.Frame);
            };

            await camera.SoftTriggerAsync(
                new CameraTriggerContext
                {
                    CameraId = "Camera01",
                    TriggerId = "trigger-background"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.False(frameSource.Task.IsCompleted, "ReturnBeforeFrameArrived should preserve async frame delivery behavior.");

            var completed = await Task.WhenAny(frameSource.Task, Task.Delay(1000)).ConfigureAwait(false);
            AssertEx.True(object.ReferenceEquals(frameSource.Task, completed), "Background fake frame should still arrive.");
            AssertEx.True(camera.LastError == null, "Background fake frame should not report adapter error.");

            var frame = await frameSource.Task.ConfigureAwait(false);
            AssertEx.Equal("trigger-background", frame.TriggerId, "Background fake frame should preserve trigger id.");
        }

        public static async Task CameraFrameRouterDuplicateRegisterDoesNotDuplicateCallbacks()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            using (var router = new DefaultCameraFrameRouter())
            using (var subscription = router.Subscribe(
                camera,
                new CameraFrameWaitTicket
                {
                    CameraId = "Camera01",
                    MatchMode = CameraFrameMatchModes.Any
                }))
            {
                var callbackCount = 0;
                subscription.FrameArrived += delegate
                {
                    Interlocked.Increment(ref callbackCount);
                };

                router.EnsureCamera(camera, "Camera01");
                router.EnsureCamera(camera, "Camera01");

                await camera.SoftTriggerAsync(
                    new CameraTriggerContext
                    {
                        CameraId = "Camera01",
                        TriggerId = "router-duplicate"
                    },
                    CancellationToken.None).ConfigureAwait(false);

                AssertEx.Equal(1, callbackCount, "Duplicate EnsureCamera calls must not duplicate camera event subscriptions.");
            }
        }

        public static async Task CameraFrameRouterUnregisterReleasesSubscription()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            using (var router = new DefaultCameraFrameRouter())
            {
                var callbackCount = 0;
                var subscription = router.Subscribe(
                    camera,
                    new CameraFrameWaitTicket
                    {
                        CameraId = "Camera01",
                        MatchMode = CameraFrameMatchModes.Any
                    });
                subscription.FrameArrived += delegate
                {
                    Interlocked.Increment(ref callbackCount);
                };

                AssertEx.True(router.UnregisterCamera("Camera01"), "UnregisterCamera should return true for a registered camera.");

                await camera.SoftTriggerAsync(
                    new CameraTriggerContext
                    {
                        CameraId = "Camera01",
                        TriggerId = "router-unregister"
                    },
                    CancellationToken.None).ConfigureAwait(false);

                AssertEx.Equal(0, callbackCount, "UnregisterCamera should unsubscribe camera frame callbacks.");
                AssertEx.False(router.UnregisterCamera("Camera01"), "UnregisterCamera should return false after the camera has already been removed.");
            }
        }

        public static async Task CameraFrameRouterDisposeCancelsWaiters()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            var router = new DefaultCameraFrameRouter();
            var waitTask = router.WaitForFrameAsync(
                camera,
                new CameraFrameWaitTicket
                {
                    CameraId = "Camera01",
                    MatchMode = CameraFrameMatchModes.TriggerId,
                    TriggerId = "never-arrives"
                },
                10000,
                CancellationToken.None);

            router.Dispose();

            await AssertEx.ThrowsAsync<OperationCanceledException>(
                async delegate
                {
                    await waitTask.ConfigureAwait(false);
                }).ConfigureAwait(false);
        }

        public static async Task CameraFrameRouterStreamSubscriptionDisposeStopsCallbacks()
        {
            var camera = new FakeCameraAdapter("Camera01")
            {
                FrameDelayMs = 1
            };
            using (var router = new DefaultCameraFrameRouter())
            {
                var callbackCount = 0;
                var subscription = router.Subscribe(
                    camera,
                    new CameraFrameWaitTicket
                    {
                        CameraId = "Camera01",
                        MatchMode = CameraFrameMatchModes.Any
                    });
                subscription.FrameArrived += delegate
                {
                    Interlocked.Increment(ref callbackCount);
                };

                subscription.Dispose();

                await camera.SoftTriggerAsync(
                    new CameraTriggerContext
                    {
                        CameraId = "Camera01",
                        TriggerId = "router-subscription-dispose"
                    },
                    CancellationToken.None).ConfigureAwait(false);

                AssertEx.Equal(0, callbackCount, "Disposed stream subscriptions must not receive frames.");
            }
        }

        public static Task VisionImageReferenceLifecycle()
        {
            var native = new DisposableNativeImage();
            var image = new VisionImageReference("image-native", 10, 20, "Mono8", new byte[] { 1, 2, 3 }, native, true, "HeightMap");
            image.Metadata[FlowMetadataKeys.CameraId] = "Camera01";

            byte[] bytes;
            AssertEx.True(image.TryGetBytes(out bytes), "VisionImageReference should expose bytes before disposal.");
            AssertEx.Equal(3, bytes.Length, "VisionImageReference should report byte length.");
            bytes[0] = 99;

            byte[] secondRead;
            AssertEx.True(image.TryGetBytes(out secondRead), "TryGetBytes should be repeatable.");
            AssertEx.Equal((byte)1, secondRead[0], "TryGetBytes should return a defensive copy.");

            var clone = image.CloneReference();
            AssertEx.False(object.ReferenceEquals(image, clone), "CloneReference should create a distinct image reference.");
            AssertEx.Equal("image-native", clone.ImageId, "CloneReference should preserve ImageId.");
            AssertEx.Equal("HeightMap", clone.ImageKind, "CloneReference should preserve ImageKind.");
            AssertEx.True(object.ReferenceEquals(image.NativeImage, clone.NativeImage), "CloneReference should preserve native image reference.");
            AssertEx.Equal("Camera01", Convert.ToString(clone.Metadata[FlowMetadataKeys.CameraId]), "CloneReference should copy metadata.");

            image.Dispose();

            AssertEx.True(image.IsDisposed, "Dispose should mark image disposed.");
            AssertEx.True(native.IsDisposed, "Dispose should release owned native image.");
            AssertEx.False(image.TryGetBytes(out bytes), "Disposed image should not expose bytes.");
            AssertEx.False(clone.IsDisposed, "CloneReference should not be disposed with the source image.");
            AssertEx.True(clone.TryGetBytes(out bytes), "CloneReference should still expose bytes after source disposal.");
            AssertEx.Equal((byte)1, bytes[0], "CloneReference should keep the referenced bytes.");
            clone.Dispose();
            return Task.FromResult(0);
        }

        public static Task FakeVisionImageLifecycle()
        {
            var native = new DisposableNativeImage();
            var image = new FakeVisionImage("fake-native", 5, 6, "RGB24", new byte[] { 7, 8 }, native, true, "TextureImage");
            image.Metadata[FlowMetadataKeys.FrameId] = "frame-001";

            var clone = image.CloneReference();
            AssertEx.False(object.ReferenceEquals(image, clone), "FakeVisionImage CloneReference should create a distinct reference.");
            AssertEx.Equal("TextureImage", clone.ImageKind, "FakeVisionImage clone should preserve ImageKind.");
            AssertEx.True(object.ReferenceEquals(image.NativeImage, clone.NativeImage), "FakeVisionImage clone should preserve native image reference.");
            AssertEx.Equal("frame-001", Convert.ToString(clone.Metadata[FlowMetadataKeys.FrameId]), "FakeVisionImage clone should copy metadata.");

            image.Dispose();

            byte[] bytes;
            AssertEx.True(native.IsDisposed, "FakeVisionImage should dispose owned native image.");
            AssertEx.False(image.TryGetBytes(out bytes), "Disposed FakeVisionImage should not expose bytes.");
            AssertEx.True(clone.TryGetBytes(out bytes), "FakeVisionImage clone should keep bytes after source disposal.");
            AssertEx.Equal(2, bytes.Length, "FakeVisionImage clone should keep byte length.");
            clone.Dispose();
            return Task.FromResult(0);
        }

        public static async Task FakeRecipeReturnsOk()
        {
            var recipe = new FakeRecipeAdapter("Recipe01");
            var result = await recipe.RunAsync(
                new RecipeRunRequest
                {
                    RecipeId = "Recipe01"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.NotNull(result, "Fake recipe should return a result.");
            AssertEx.True(result.IsSuccess, "Fake recipe should succeed.");
            AssertEx.Equal("OK", result.Status, "Fake recipe status should be OK.");
            AssertEx.Equal("Recipe01", Convert.ToString(result.Outputs["RecipeId"]), "Fake recipe output should include RecipeId.");
        }

        public static async Task FakeImageSaveReturnsPath()
        {
            var saver = new FakeImageSaveAdapter("ImageSave01");
            var result = await saver.SaveAsync(
                new ImageSaveRequest
                {
                    Image = new FakeVisionImage("image-001", 320, 240, "Mono8", null),
                    FileName = "part-a",
                    Format = "bmp"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.NotNull(result, "Fake image saver should return a result.");
            AssertEx.True(result.IsSuccess, "Fake image saver should succeed.");
            AssertEx.True(result.Path.IndexOf("fake://images", StringComparison.OrdinalIgnoreCase) == 0, "Fake image saver should use the fake base path.");
            AssertEx.True(result.Path.EndsWith("/part-a.bmp", StringComparison.OrdinalIgnoreCase), "Fake image saver should return a simulated file path.");
        }

        public static async Task FakeImageSaveSnapshotsImageReference()
        {
            var saver = new FakeImageSaveAdapter("ImageSave01");
            var image = new FakeVisionImage("image-snapshot", 320, 240, "Mono8", new byte[] { 1, 2, 3, 4 });

            var result = await saver.SaveAsync(
                new ImageSaveRequest
                {
                    Image = image,
                    FileName = "part-b",
                    Format = "png"
                },
                CancellationToken.None).ConfigureAwait(false);

            AssertEx.Equal(4, Convert.ToInt32(result.Metadata[FlowMetadataKeys.ByteLength], CultureInfo.InvariantCulture), "Fake saver should record byte length.");
            AssertEx.Equal(false, Convert.ToBoolean(result.Metadata[FlowMetadataKeys.HasNativeImage], CultureInfo.InvariantCulture), "Fake saver should record native image state.");
            AssertEx.Equal("Raw", Convert.ToString(result.Metadata[FlowMetadataKeys.ImageKind], CultureInfo.InvariantCulture), "Fake saver should record image kind.");

            var savedRequests = saver.SnapshotSavedRequests();
            AssertEx.Equal(1, savedRequests.Count, "Fake saver should snapshot one request.");
            AssertEx.False(object.ReferenceEquals(image, savedRequests[0].Image), "Fake saver snapshot should clone the image reference.");
            image.Dispose();

            byte[] bytes;
            AssertEx.False(image.TryGetBytes(out bytes), "Source image should be disposed.");
            AssertEx.False(savedRequests[0].Image.IsDisposed, "Snapshot image reference should remain usable after source disposal.");
            AssertEx.True(savedRequests[0].Image.TryGetBytes(out bytes), "Snapshot image reference should expose bytes.");
            AssertEx.Equal(4, bytes.Length, "Snapshot image reference should keep bytes.");
        }

        private sealed class DisposableNativeImage : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
