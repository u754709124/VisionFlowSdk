# 09 - Release and Integration

## MVision 环境变量接入

上位机加载 `.flowruntime` 后枚举 `RuntimeFlowDefinition.EnvironmentVariables`，
将每项用户输入初始化为 `DefaultValue` 并要求用户提供完整有效值；启动流程时按
稳定 `Id` 组装字典赋给 `FlowExecutionOptions.EnvironmentVariableValues`。
运行层在缺少覆盖时回退默认值，以支持通用宿主。

`RuntimeFlowDefinition.GlobalVariables` 不走环境覆盖。宿主创建 `FlowRunner` 时，SDK
仅按流程定义默认值建立 Session 存储；运行值不会写回流程文件或跨 Runner 继承。
配方重载、切换或其它需要重置 Session 的场景应停止旧 Runner 并创建新 Runner。

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

需要嵌入设计器时再引用：

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

项目专属设备节点应通过 `IDeviceRegistry` 获取相机、单光源控制器或运控 Adapter。
具体厂商 SDK、组合光源映射、运控协议、命令枚举和 Adapter 实现随上位机项目发布，
不进入 Core。

## Flow Files

`.flowdesign` 只用于设计器编辑和发布。生产部署 `.flowruntime`，并确保其中不含节点坐标、画布缩放、WPF 样式或设计器状态。

示例流程：

```text
core-basic.flowdesign
core-basic.flowruntime
```

## Integration Notes

项目专属节点应：

- 使用 Core Adapter 契约或项目自己的兼容契约访问上位机能力。
- 自行定义 NodeType、Descriptor、Config 和测试。
- 实例端口、设置或输出会随命令变化时，实现 `IInstanceNodeDescriptorProvider`，并通过 `NodeRegistry.ResolveDescriptor(NodeDefinition)` 使用实例契约；静态 Descriptor 继续作为节点库契约。
- 对长耗时任务使用异步任务、有界队列和取消令牌。
- 在发布前通过 `FlowValidator` 和项目专属测试验证。
