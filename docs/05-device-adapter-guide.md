# 05 - Device Adapter Guide

## 目的

Adapter 契约让 Core 相机节点和项目专属节点可以调用现有上位机设备逻辑，同时避免 Core 直接引用具体 SDK。

## 当前边界

Core 保留相机和图像基础契约：

- `IDeviceRegistry`
- `ICameraAdapter`
- `CameraFrameData`
- `IVisionImage`

Core 内置三个通用相机节点：`camera.soft_trigger`、`camera.hard_trigger`、`camera.parameter.set`。真实相机 SDK、Fake 设备、Demo 设备和项目专属算法/保存/数据库节点仍由具体项目实现。

光源、运控、Recipe、图像保存、数据库保存和队列服务不作为 Core 公共契约发布；项目如需这些能力，应在项目专属节点库或上位机应用中定义自己的接口。

## ICameraAdapter

`ICameraAdapter` 负责包装真实相机或测试桩：

```csharp
Task<CameraFrameData> GrabOneAsync(CancellationToken cancellationToken = default(CancellationToken));
event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;
Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken);
IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors();
```

`GrabOneAsync` 用于软触发节点单次采集。`FrameArrived` 用于硬触发节点订阅外部硬触发图像回调。参数设置节点只允许写入 `CameraParameterDescriptor.IsWritable=true` 的参数。

## 规则

- 节点不直接调用真实 SDK。
- 节点通过 Adapter 接口访问设备或上位机服务。
- Adapter 负责包装真实 SDK、旧服务或测试桩。
- 相机回调线程只做轻量封装，不执行后续节点或重算法。
- 图像对象通过 `IVisionImage` 或项目自有兼容实现流转。
- 长耗时 Adapter 操作必须支持 `CancellationToken` 和超时策略。

## 图像生命周期

当图像跨异步任务、队列或延迟保存边界时，应使用 `IVisionImage.CloneReference()` 或项目自有引用计数机制保持底层句柄有效。拥有原生句柄的一方负责释放。
