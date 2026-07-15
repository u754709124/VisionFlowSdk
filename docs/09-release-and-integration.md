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

## Embedded Designer Wiring

当业务应用需要把流程图和自己的策略、配方或其它元数据保存在同一文件中时，由宿主管理外层文件，设计器只负责 `FlowDesignDocument`：

```csharp
var nodes = new NodeRegistry();
CommonNodeRegistration.RegisterAll(nodes);
// 在这里继续注册项目专属节点工厂。

var designer = new FlowDesignerControl(nodes, null, new FlowDesignerOptions
{
    LoadSampleOnStartup = false,
    ShowStandaloneDocumentCommands = false
});

await designer.LoadDocumentAsync(flowDesignFromHostFile);
var flowDesignForSave = designer.CaptureDocument();
```

新建宿主文件时可调用 `ResetDocumentAsync(flowId, flowName)` 创建空白图。宿主保存前必须调用 `CaptureDocument()`，不要长期持有并直接修改早先传入的对象。

外层文件若要嵌入流程 JSON，应先使用 `FlowDesignSerializer.Serialize(flowDesignForSave)` 生成 SDK 协议 JSON，再把结果作为 JSON 对象嵌入；不要把它保存为转义后的 JSON 字符串，也不要由其它序列化器直接重写流程协议字段。

## Publishing Runtime Files

嵌入 Designer 的宿主可直接发布当前画布：

```csharp
var result = designer.PublishRuntimeFile(@"C:\Flows\Station01.flowruntime");
if (!result.IsSuccess)
{
    ShowValidationIssues(result.Validation.Issues);
}
```

不承载 Designer 控件的发布工具可直接使用 Core 服务：

```csharp
var publisher = new FlowPublishService(nodes);
var result = publisher.PublishToFile(flowDesignDocument, @"C:\Flows\Station01.flowruntime");
```

两种入口执行同一条发布链：Schema v2 检查、运行态深拷贝、`FlowValidator` 校验、移除设计器 ViewState、序列化 `.flowruntime`。校验失败不会创建或覆盖目标文件；目标路径必须使用 `.flowruntime` 扩展名。生产部署不应直接复制 `.flowdesign` 或从 Designer 内部对象读取运行定义。

## Runtime Wiring

```csharp
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Nodes;

var nodes = new NodeRegistry();
CommonNodeRegistration.RegisterAll(nodes);

// 具体项目在这里注册自己的相机、算法、保存、数据库等节点。
nodes.Register(new StationRecipeNodeFactory(existingRecipeSystem));

var devices = new StationDeviceRegistry(existingCameraAdapters);
var flow = RuntimeFlowSerializer.Load("Station01.flowruntime");
var eventSink = new StationEventSink();
var runner = new FlowEngine(nodes, eventSink, devices).CreateRunner(flow);
```

项目专属相机节点应通过 `IDeviceRegistry` 获取 `ICameraAdapter`，并在自己的节点库中实现软触发、硬触发监听或参数设置等行为。

## Flow Files

`.flowdesign` 只用于设计器编辑和调试发布。生产部署 `.flowruntime`，并确保其中不含节点坐标、画布缩放、WPF 样式或设计器状态。

示例流程：

```text
core-basic.flowdesign
core-basic.flowruntime
```

## Integration Notes

项目专属节点应：

- 使用 Core Adapter 契约或项目自己的兼容契约访问上位机能力。
- 自行定义 NodeType、Descriptor、Config 和测试。
- 对长耗时任务使用异步任务、有界队列和取消令牌。
- 在发布前通过 `FlowValidator` 和项目专属测试验证。
