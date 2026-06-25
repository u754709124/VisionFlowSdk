# 05 - Device Adapter Guide

## 目的

Adapter 让流程 Runtime 能调用现有上位机设备、算法、保存、数据库逻辑，同时不直接依赖具体 SDK。

## 原则

```text
节点不调用真实 SDK。
节点调用 Adapter 接口。
Adapter 调用真实 SDK 或旧上位机 Service。
```

## 上位机真实适配示例

上位机项目中实现：

```text
UpperMachineCameraAdapter : ICameraAdapter
UpperMachineLightAdapter : ILightAdapter
UpperMachineMotionAdapter : IMotionAdapter
UpperMachineRecipeAdapter : IRecipeAdapter
UpperMachineImageSaveAdapter : IImageSaveAdapter
UpperMachineDatabaseAdapter : IDatabaseAdapter
```

## SDK 内部 Fake Adapter 用途

Fake Adapter 用于：

- 单元测试
- 集成测试
- WinForms Demo
- Designer Demo
- 离线 UI 调试

## Adapter 规则

- SDK 具体调用只在 Adapter 中。
- 不直接将 SDK 图像对象泄漏给节点，除非已被 `IVisionImage` 包装。
- 相机回调事件应快速返回。
- Metadata 至少包含 CameraId、FrameId、TriggerId、GrabTime。
- 支持 CancellationToken。
- 支持必要的超时策略。

## Camera Adapter 建议接口

```csharp
public interface ICameraAdapter
{
    string CameraId { get; }

    IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors();

    Task SetParameterAsync(
        string parameterName,
        object value,
        CancellationToken cancellationToken);

    Task<object> GetParameterAsync(
        string parameterName,
        CancellationToken cancellationToken);

    Task SoftTriggerAsync(
        CameraTriggerContext triggerContext,
        CancellationToken cancellationToken);

    event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;
}
```
## 2026-06 Adapter Notes

- `IMotionAdapter` now carries richer `MotionMessage` and `MotionEventArgs` data for position, capture group, scan group, payload, and timeout-aware motion operations.
- Fake motion updates position state and emits events so motion nodes can be tested without production hardware.
- Fake camera callback delivery is asynchronous and cancellation-aware. It no longer relies on unsafe fire-and-forget callback behavior for tests and demos.
- Camera frame routing is a runtime service. Production hosts can use the default router or provide their own `ICameraFrameRouter` when existing acquisition pipelines already buffer frames.
- `IVisionImage` implementations must honor `Dispose`, `CloneReference`, and `TryGetBytes`. Native SDK image handles should be wrapped behind `NativeImage` or an adapter-owned image implementation instead of leaking SDK types into nodes.
