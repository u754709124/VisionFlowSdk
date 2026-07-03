# VisionFlowSdk

`VisionFlowSdk` 是一个 UI 无关的工业视觉流程运行 SDK。当前 SDK 内置内容已收缩为：

- `Vision.Flow.Core`：流程定义、执行引擎、发布/序列化、运行事件、变量池、Adapter 契约和基础流程节点。
- `Vision.Flow.Designer.Wpf`：WPF 流程设计器，用于编辑、调试和发布 `.flowruntime`。

相机节点、算法节点、图像保存、数据库保存、拼图、扫描和融合等节点不再由 SDK 内置提供，应放在具体上位机项目或项目专属节点库中实现和注册。

## 生产运行路径

```text
.flowruntime
  -> FlowRunner
  -> Core built-in nodes
  -> project-specific nodes
  -> upper-machine devices / algorithms / storage
```

生产 WinForms 上位机至少引用 `Vision.Flow.Core.dll`，并按需引用自己的项目专属节点库。需要嵌入设计器或调试工具时再引用 `Vision.Flow.Designer.Wpf.dll`。

Core Adapter 公共面保留相机查找、相机适配器、相机帧和图像引用契约。相机节点不再由 Core 内置，需由具体项目或项目专属节点库通过这些契约实现。光源、运控、Recipe、保存、数据库、队列以及扫描/融合分组能力也由具体项目或项目专属节点库自行定义。

## 公共 API 命名空间

Core 公共类型按职责拆分命名空间，生产运行常用引用如下：

```csharp
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Nodes;
```

嵌入 WPF 设计器时使用：

```csharp
using Vision.Flow.Designer.Wpf.Controls;
```

## 解决方案结构

```text
src/Vision.Flow.Core
src/Vision.Flow.Designer.Wpf

tests/Vision.Flow.Tests

demos/Vision.Flow.Demo.WinForms
demos/Vision.Flow.Demo.DesignerWpf

samples/flows
build
docs
```

## Core 内置节点

`CommonNodeRegistration.RegisterAll` 注册以下 Core 内置节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

这些类型位于 `Vision.Flow.Core.dll`，源码命名空间保留为 `Vision.Flow.Nodes`。相机、算法、保存和数据库等项目节点应由项目节点库提供自己的 NodeType、Descriptor、Factory 和测试。

## 构建、测试、打包

```powershell
./build/build.ps1
./build/test.ps1
./build/pack-sdk.ps1
```

打包产物位于：

```text
artifacts/sdk
artifacts/samples/flows
```

## 示例流程

当前仓库示例只保留 Core 基础流程：

```text
samples/flows/core-basic.flowdesign
samples/flows/core-basic.flowruntime
```
