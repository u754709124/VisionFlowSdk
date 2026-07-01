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

负责流程定义、运行态模型、节点接口、执行引擎、变量池、运行事件、校验、发布、序列化、Adapter 契约和 Core 基础节点。

Core 内置节点只包含：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

设备、算法、存储、拼图、融合等节点由具体项目实现，并通过 `NodeRegistry` 注册。

### Vision.Flow.Designer.Wpf

负责 WPF 设计器 UI：节点库、画布、连线、属性面板、变量选择器、调试面板、`.flowdesign` 保存/加载和 `.flowruntime` 发布。

Designer 默认使用 Core 基础节点库，也允许宿主注入包含项目专属节点的 `NodeRegistry`。

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
  -> load .flowruntime
  -> create FlowRunner
  -> trigger entries from station events
  -> consume FlowRuntimeEvent
```

生产环境不创建 Designer 控件，不依赖画布、节点卡片或 ViewModel。

## Runtime 服务

Core 仍提供 `IDeviceRegistry`、`ICameraFrameRouter`、`IFlowTaskQueueRegistry`、`IVisionImage` 等契约，供项目专属节点复用。SDK 不再内置使用这些契约的设备节点。
