# 09 - Release and Integration

## SDK 输出

SDK 打包后输出 DLL：

```text
Vision.Flow.Core.dll
Vision.Flow.Nodes.dll
Vision.DeviceAdapters.dll
Vision.Flow.Designer.Wpf.dll
```

生产上位机通常只引用：

```text
Vision.Flow.Core.dll
Vision.Flow.Nodes.dll
Vision.DeviceAdapters.dll
```

需要编辑/调试流程时才额外引用：

```text
Vision.Flow.Designer.Wpf.dll
```

## 生产运行集成方式

上位机应用：

1. 初始化现有设备系统。
2. 创建真实 Adapter。
3. 注册 Adapter。
4. 注册公共 NodeFactory。
5. 加载 `.flowruntime`。
6. 创建 `FlowRunner`。
7. 从运控、相机、IO 事件触发入口。
8. 监听 RuntimeEvent。

示例：

```csharp
var devices = new DefaultDeviceRegistry();

devices.RegisterCamera("Camera01", new UpperMachineCameraAdapter(existingCamera));
devices.RegisterLight("Light01", new UpperMachineLightAdapter(existingLight));
devices.RegisterMotion("Motion01", new UpperMachineMotionAdapter(existingMotion));

var nodes = new NodeRegistry();
CommonNodeRegistration.RegisterAll(nodes);

var flow = RuntimeFlowSerializer.Load("Station01.flowruntime");

var engine = new FlowEngine(nodes, devices);
var runner = engine.CreateRunner(flow);

runner.RuntimeEventReceived += OnRuntimeEvent;

await runner.StartAsync(CancellationToken.None);
```

## WinForms 中宿主 WPF Designer

如果需要编辑流程，可通过 `ElementHost` 宿主 WPF Designer。

生产模式不要创建 Designer 控件。
