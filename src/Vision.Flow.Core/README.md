# Vision.Flow.Core 开发与集成手册

`Vision.Flow.Core` 是 `VisionFlowSdk` 的 UI 无关运行时核心。它面向生产上位机和项目专属节点库，提供流程定义、运行态模型、节点契约、执行引擎、变量池、运行事件、校验、发布、序列化和 Adapter 契约。

本 README 是 Core 被引用到其他解决方案时的稳定边界说明。未在本文档中列为稳定边界的节点、UI 类型或 Demo 类型，不应被外部项目当成长期公共契约依赖。

## 引用方式

生产项目只引用：

```text
Vision.Flow.Core
```

不要在生产运行项目中引用：

```text
Vision.Flow.Designer.Wpf
Vision.Flow.Demo.WinForms
Vision.Flow.Demo.DesignerWpf
WPF Canvas / 节点卡片 / Designer ViewModel
```

如果把 Core 源码混入其他解决方案共同开发，建议在 Visual Studio 中使用“添加现有项目”引用：

```text
D:\Code\VisionFlowSdk\src\Vision.Flow.Core\Vision.Flow.Core.csproj
```

如果以二进制方式集成，生产上位机只部署 `Vision.Flow.Core.dll`。设计器、Demo 和调试工具应独立于生产运行进程。

## 依赖边界

Core 允许包含：

- 纯 C# 流程模型和运行态模型。
- `IFlowNode`、`INodeFactory`、`NodeRegistry` 等节点契约。
- `FlowRunner`、变量池、运行事件、校验、发布和序列化逻辑。
- Adapter 抽象接口和运行时扩展服务。
- Core 稳定基础流程节点。

Core 禁止包含：

- WPF、WinForms 或 Designer UI 引用。
- 具体相机、光源、运控等设备 SDK 引用。
- 上位机业务代码。
- 项目专属设备节点、算法节点、保存节点、数据库节点、拼图、扫描或融合节点。
- 依赖节点卡片、画布坐标、Designer ViewModel 的生产运行逻辑。

允许的依赖方向：

```text
Designer.Wpf -> Flow.Core
Demo.WinForms -> Flow.Core
Demo.DesignerWpf -> Flow.Core
Demo.DesignerWpf -> Designer.Wpf
Tests -> Flow.Core
ProjectSpecificNodes -> Flow.Core
```

禁止的依赖方向：

```text
Flow.Core -> WPF
Flow.Core -> WinForms
Flow.Core -> specific SDK
Runtime -> Designer UI
Production Runtime -> WPF Canvas
```

## 生产运行接入

生产上位机必须加载 `.flowruntime` 并通过 `FlowRunner` 执行，不打开 WPF 流程设计器。

推荐接入流程：

```text
.flowruntime
  -> 注册 Core 基础节点工厂
  -> 注册项目专属节点工厂
  -> 注册设备或服务 Adapter
  -> 创建 FlowRunner
  -> 从外部事件触发流程入口
  -> 订阅 FlowRuntimeEvent
```

常用命名空间：

```csharp
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Nodes;
```

接入时应由上位机项目负责注册自己的设备、算法、保存、数据库等能力。Core 只负责流程运行和公共契约，不直接持有真实设备 SDK 或业务服务实现。

## Core 稳定内置节点

外部项目可以依赖的 Core 稳定基础节点只有：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

这些节点源码命名空间保留为 `Vision.Flow.Nodes`，编译产物属于 `Vision.Flow.Core.dll`。

项目专属节点类型建议增加项目或领域前缀，例如：

```text
station.recipe.run
station.image.save
station.database.save
station.fusion.final
```

不要把项目专属 `NodeType` 常量、节点工厂或节点实现放回 Core。

## 节点开发规则

每个新节点必须包含：

```text
{NodeName}NodeConfig
{NodeName}Node
{NodeName}NodeFactory
{NodeName}Descriptor
```

节点还必须声明：

- 输入端口和输出端口。
- 输出变量。
- 必要设置项及其数据类型。
- 运行成功、失败、超时和取消行为。
- 单元测试或集成测试。

运行节点实现 `IFlowNode`。需要在流程启动时订阅外部事件的节点实现 `IFlowListenerNode`，并在停止时释放订阅资源。

Descriptor 必须包含：

- `NodeType`
- `DisplayName`
- `Category`
- `Version`
- `InputPorts`
- `OutputPorts`
- `Settings`
- `Outputs`

Designer 的节点库、属性面板和变量选择器都依赖 Descriptor。节点卡片只展示摘要，详细配置应放在宿主设计器的属性面板中。

## Adapter 规则

节点必须通过 `Vision.Flow.Core` 中定义的 Adapter 接口或项目自己的兼容契约访问设备和上位机能力。

正确做法：

```csharp
var camera = context.Devices.GetCamera(cameraId);
var frame = await camera.GrabOneAsync(cancellationToken);
```

错误做法：

```csharp
var camera = new HikCamera();
camera.MV_CC_SetCommandValue_NET(...);
```

Adapter 负责包装真实 SDK、旧上位机服务或测试桩。真实设备集成、Fake Adapter、Demo 设备和项目专属节点应由具体上位机应用或单独项目实现，不放进 Core。

线程和设备访问要求：

- 异步 API 使用 `async` / `await` 和 `CancellationToken`。
- 设备操作应支持超时。
- 相机或设备回调线程只做轻量封装并快速返回。
- 重算法、保存、数据库等长耗时工作放入异步任务或有界队列。
- 不阻塞 UI 线程，不在 SDK 回调线程执行重算法。

## 流程文件规则

流程文件分为两类：

```text
.flowdesign
  设计态文件，包含运行态流程和设计器视图状态。

.flowruntime
  生产运行态文件，只包含运行时节点、连线、入口、配置和变量绑定。
```

`.flowruntime` 禁止包含：

- 节点 X/Y 坐标。
- Canvas 缩放或偏移。
- WPF 样式。
- Debug-only UI 状态。
- WPF 类型名。
- Designer ViewModel 状态。

数据主要通过变量绑定传递：

```text
{{ set_result.Value }}
{{ token.TokenId }}
```

执行线表示控制流，变量池承载节点之间的数据传递。项目专属节点可以扩展自己的 `settings` 和 `inputBindings` 协议字段，但必须保持 `.flowruntime` 与 UI 状态解耦。

## 开发检查清单

修改 Runtime、Node、Serialization 或 Compiler 时，必须同步检查：

- 是否仍能在无 WPF Designer 的生产进程中执行。
- 是否仍通过 `FlowRunner`、`NodeRegistry` 和 Adapter 契约接入。
- 是否没有新增 Core 到 UI、具体 SDK 或上位机业务项目的反向依赖。
- 是否更新或新增相关测试。
- 是否保持 `.flowruntime` 不包含设计器状态。
- 是否把领域字符串集中定义，例如 NodeType、端口名、变量输出名、设置键、事件 Data 键、校验错误码和文件扩展名。

公共模型类和关键属性应补充中文注释，说明用途、运行态/设计态边界、序列化影响、上位机集成含义和兼容性约束。注释只说明职责边界、复杂流程、线程/队列约束、设备适配边界和不明显的业务规则，不逐行复述代码。

## 测试与验证

测试项目：

```text
tests/Vision.Flow.Tests
```

优先使用脚本：

```powershell
./build/build.ps1
./build/test.ps1
./build/clean.ps1
```

修改执行引擎时，应验证：

- 节点执行顺序。
- 错误事件。
- 取消或超时逻辑。
- 分支、汇合、Continuation 和监听节点生命周期。

修改 Core 基础节点时，应验证：

- 节点注册。
- 输出变量。
- 运行事件。
- Error / Timeout 路由。
- 序列化和发布兼容性。

修改序列化或发布逻辑时，应验证：

- `.flowdesign` round-trip。
- `.flowruntime` round-trip。
- 发布后移除 view state。
- `.flowruntime` 不包含 WPF 或 Designer 状态。

项目专属节点应在对应项目增加测试，并使用项目自己的 Fake Adapter 或测试桩。

## 文档维护

行为变化时同步更新仓库文档：

```text
docs/01-architecture.md
docs/02-flow-file-format.md
docs/03-runtime-design.md
docs/04-node-development-guide.md
docs/05-device-adapter-guide.md
docs/07-test-plan.md
docs/09-release-and-integration.md
```

Core 被其他解决方案引用时，以本 README 作为快速入口；完整 SDK 背景和演进说明仍以根目录 README 与 `docs/` 为准。
