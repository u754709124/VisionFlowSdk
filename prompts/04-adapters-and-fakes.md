# 04 - 设备适配器接口和 Fake 设备

请实现阶段 4：设备适配器接口和 Fake Adapter。

请先阅读：

- AGENTS.md
- docs/05-device-adapter-guide.md

在 Vision.Flow.Core 中定义接口：

- IDeviceRegistry
- ICameraAdapter
- ILightAdapter
- IMotionAdapter
- IRecipeAdapter
- IImageSaveAdapter
- IDatabaseAdapter
- IVisionImage

定义必要的数据类型：

- CameraTriggerContext
- CameraFrameArrivedEventArgs
- CameraFrameData
- CameraParameterDescriptor
- LightChannelSetting
- RecipeRunRequest
- RecipeRunResult
- ImageSaveRequest
- ImageSaveResult
- DatabaseSaveRequest

在 Vision.DeviceAdapters 中实现：

- DefaultDeviceRegistry
- FakeVisionImage
- FakeCameraAdapter
- FakeLightAdapter
- FakeMotionAdapter
- FakeRecipeAdapter
- FakeImageSaveAdapter
- FakeDatabaseAdapter

FakeCameraAdapter 要求：

1. 支持 SetParameterAsync。
2. 支持 SoftTriggerAsync。
3. SoftTriggerAsync 后异步触发 FrameArrived 事件。
4. 产生 FakeVisionImage。
5. Frame metadata 包含 CameraId、TriggerId、FrameId、GrabTime。

测试要求：

1. 注册 FakeCameraAdapter 后能通过 DeviceRegistry 获取。
2. SoftTriggerAsync 后能收到 FrameArrived。
3. FakeRecipeAdapter 能返回 OK 结果。
4. FakeImageSaveAdapter 能返回模拟路径。

验收条件：

1. build/test.ps1 通过。
2. 不引入真实 SDK。
3. DeviceAdapters 只依赖 Flow.Core。
