# 05 - Device Adapter Guide

## 目的

Adapter 契约让项目专属节点可以调用现有上位机设备逻辑，同时避免 Core 直接引用具体 SDK。

## 当前边界

Core 保留相机、运控和图像基础契约：

- `IDeviceRegistry`
- `ILightControllerAdapter` 与相机、运控 Adapter 同属 Core 设备契约；通过
  `IDeviceRegistry.GetLightController` / `TryGetLightController` 按稳定控制器 Id 获取。
- `ICameraAdapter`
- `IMotionAdapter`
- `CameraFrameData`
- `IVisionImage`

Core 不再内置相机节点。真实相机 SDK、Fake 设备、Demo 设备、相机节点和项目专属算法/保存/数据库节点均由具体项目实现。

Core 的运控契约只使用逻辑命令字符串和通用参数字典，不包含任何项目专属命令枚举、
线缆协议、命令目录或设备 SDK。Core 光源契约只描述单控制器通道、模式与独占租约；
组合映射、Recipe、图像保存、数据库保存和队列服务不作为 Core 公共契约发布，项目应在
项目专属节点库或上位机应用中定义这些接口。

## ICameraAdapter

`ICameraAdapter` 负责包装真实相机或测试桩：

```csharp
Task<CameraFrameData> GrabOneAsync(CancellationToken cancellationToken = default(CancellationToken));
event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;
Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken);
IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors();
```

`GrabOneAsync` 可用于项目专属软触发节点单次采集。`FrameArrived` 可用于项目专属硬触发节点订阅外部硬触发图像回调。参数设置节点只允许写入 `CameraParameterDescriptor.IsWritable=true` 的参数。

`CameraFrameData.CaptureFrameId` 是 `Int32` 相机采集帧序号，`0` 表示尚未分配，
有效序号从 `1` 开始；它不得用于保存批号、载具码或待检品框架标识。待检品业务元数据
应由具体项目在独立业务上下文中定义。

需要允许流程重置帧序号的 Adapter 可实现 `ICameraFrameSequenceAdapter`。
`ResetCaptureFrameSequence()` 必须与帧序号分配互斥，将当前值复位为 `0`，并返回复位前
最后分配的序号。该能力只定义通用 Adapter 契约，具体计数时机和节点仍由上位机项目实现。

## IMotionAdapter

`IMotionAdapter` 通过逻辑命令名发送命令并发布接收事件。具体项目的 Adapter 负责把逻辑命令转换为项目协议枚举和线缆消息：

```csharp
Task<MotionAdapterCommandResult> SendCommandAsync(
    MotionAdapterCommandRequest request,
    CancellationToken cancellationToken);
event EventHandler<MotionAdapterCommandReceivedEventArgs> CommandReceived;
```

`MotionAdapterCommandRequest.ResponseTimeout` 表示调用方要求的响应超时。Adapter 必须继续传递 `CancellationToken`，不得让 Core 或项目节点直接引用具体运控实现程序集。

## 规则

- 节点不直接调用真实 SDK。
- 节点通过 Adapter 接口访问设备或上位机服务。
- Adapter 负责包装真实 SDK、旧服务或测试桩。
- 相机回调线程只做轻量封装，不执行后续节点或重算法。
- 图像对象通过 `IVisionImage` 或项目自有兼容实现流转。
- 长耗时 Adapter 操作必须支持 `CancellationToken` 和超时策略。

## 图像生命周期

当图像跨异步任务、队列或延迟保存边界时，应使用 `IVisionImage.CloneReference()` 或项目自有引用计数机制保持底层句柄有效。拥有原生句柄的一方负责释放。

## 可编程光源模式

LightOperatingMode 追加 Programmable=2，保留 Continuous=0、Strobe=1 及既有字符串。
直接通过 ILightControllerAdapter.Supports(Programmable) 查询能力，通过原
ILightControllerControlLease.SwitchModeAsync(Programmable, cancellationToken) 切换，
CurrentMode 可回报 Programmable，不引入额外模式接口或第二套枚举。

模式切换能力不代表支持 ReadAsync、ApplyAsync 或 TurnOffAsync：
这些操作应继续遵守各自的能力范围，不支持时明确拒绝，禁止将 Programmable
误作 Strobe 处理。步序表及地址设置属于具体设备能力，不在此次契约中新增。

上位机必须部署匹配的新 Core 程序集；使用旧模式的流程文件无需迁移。
