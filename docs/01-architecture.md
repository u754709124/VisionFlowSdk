# 01 - Architecture

## 解决方案结构

```text
src/Vision.Flow.Core
src/Vision.Flow.Designer.Wpf

tests/Vision.Flow.Tests
demos/Vision.Flow.Demo.WinForms
demos/Vision.Flow.Demo.DesignerWpf
samples/flows
```

## 项目职责

### Vision.Flow.Core

负责流程定义、运行态模型、节点接口、执行引擎、变量池、运行事件、校验、发布、序列化、Adapter 契约和 Core 内置节点。

公共 API 按职责放在 `Vision.Flow.Core.Domain.Flows`、`Vision.Flow.Core.Contracts.Nodes`、`Vision.Flow.Core.Runtime.Engine / Vision.Flow.Core.Runtime.Execution / Vision.Flow.Core.Runtime.State`、`Vision.Flow.Core.Services.Serialization`、`Vision.Flow.Core.Services.Validation` 等命名空间中。内置节点源码命名空间仍为 `Vision.Flow.Nodes`，编译产物仍属于 `Vision.Flow.Core.dll`。

Core 内置节点包含：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

Core 只内置 UI 无关基础流程节点，不引用具体相机 SDK 或上位机业务项目。相机、算法、存储、拼图、扫描、融合等项目专属节点由具体项目实现，并通过 `NodeRegistry` 注册。

### Vision.Flow.Designer.Wpf

负责 WPF 设计器 UI：节点库、画布、连线、属性面板、变量选择器、调试面板、`.flowdesign` 保存/加载和 `.flowruntime` 发布。

设计器控件公开在 `Vision.Flow.Designer.Wpf.Controls`，ViewModel 放在 `Vision.Flow.Designer.Wpf.ViewModels`。

Designer 默认使用 Core 内置节点库，也允许宿主注入包含项目专属节点的 `NodeRegistry`。

## 依赖方向

允许：

```text
Designer.Wpf -> Core
Demo.WinForms -> Core
Demo.DesignerWpf -> Core
Demo.DesignerWpf -> Designer.Wpf
Tests -> Core
Tests -> Designer.Wpf
ProjectSpecificNodes -> Core
```

禁止：

```text
Core -> WPF
Core -> WinForms
Core -> specific SDK
Production Runtime -> Designer UI
```

## 生产运行

```text
Upper-machine app
  -> register Core built-in node factories
  -> register project-specific node factories
  -> register device adapters
  -> load .flowruntime
  -> create FlowRunner
  -> start listener nodes
  -> trigger entries or dispatch continuations from station events
  -> consume FlowRuntimeEvent
```

生产环境不创建 Designer 控件，不依赖画布、节点卡片或 ViewModel。

## Runtime 服务

Core 保留 `IDeviceRegistry`、`ICameraAdapter`、`CameraFrameData` 和 `IVisionImage` 等相机/图像基础契约。项目专属相机节点应通过这些 Adapter 契约调用 `GrabOneAsync`、订阅 `FrameArrived` 或写入可写参数，不直接引用具体相机 SDK。

光源、运控、Recipe、保存、数据库、队列和扫描/融合分组能力由项目专属节点库自行定义和注册。
