# 09 - Release and Integration

## SDK Package

```powershell
./build/pack-sdk.ps1
```

产物：

```text
artifacts/sdk
artifacts/samples/flows
```

生产上位机通常只引用：

```text
Vision.Flow.Core.dll
```

需要嵌入设计器或调试工具时再引用：

```text
Vision.Flow.Designer.Wpf.dll
```

## Runtime Wiring

```csharp
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Nodes;

var nodes = new NodeRegistry();
CommonNodeRegistration.RegisterAll(nodes);

// 具体项目在这里注册自己的设备、算法、保存、数据库等节点。
nodes.Register(new StationCameraTriggerNodeFactory(existingCamera));
nodes.Register(new StationRecipeNodeFactory(existingRecipeSystem));

var flow = RuntimeFlowSerializer.Load("Station01.flowruntime");
var eventSink = new StationEventSink();
var runner = new FlowRunner(flow, nodes, eventSink);
```

如果项目专属节点需要设备、相机帧路由或队列服务，可以使用 `FlowRunner` / `FlowEngine` 的重载传入：

- `IDeviceRegistry`
- `ICameraFrameRouter`
- `FlowExecutionOptions`

## Flow Files

`.flowdesign` 只用于设计器编辑和调试发布。生产部署 `.flowruntime`，并确保其中不含节点坐标、画布缩放、WPF 样式或设计器状态。

示例流程：

```text
core-basic.flowdesign
core-basic.flowruntime
```

## Integration Notes

设备/算法节点已经迁出 SDK。项目专属节点应：

- 使用 Core Adapter 契约或项目自己的兼容契约访问上位机能力。
- 自行定义 NodeType 常量、Descriptor、Config 和测试。
- 对长耗时任务使用异步任务、有界队列和取消令牌。
- 在发布前通过 `FlowValidator` 和项目专属测试验证。
