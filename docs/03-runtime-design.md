# 03 - Runtime Design

## Runtime 目标

Runtime 必须能在没有 WPF 流程图的生产环境中执行 `.flowruntime`。

## 核心类型

```text
FlowEngine
FlowRunner
FlowExecutionContext
FlowToken
VariablePool
NodeRegistry
DeviceRegistry
FlowRuntimeEvent
```

## 执行模型

流程包含：

- Nodes
- Edges
- Entries

外部事件触发入口：

```text
TriggerAsync(entryName, token)
    -> 查找 Entry.TargetNodeId
    -> 执行节点
    -> 根据 OutputPort 查找后续边
    -> 执行下游节点
```

## FlowToken

`FlowToken` 表示一次逻辑执行上下文。

常用字段：

- ProductId
- WorkpieceId
- PositionId
- CaptureGroupId
- ScanGroupId
- FrameId
- Metadata

## VariablePool

每个节点执行后可以输出变量：

```text
camera_callback_1.Image
camera_callback_1.Frame
recipe_1.Result
fusion_1.Final3DImage
```

下游节点通过变量绑定引用：

```text
{{ camera_callback_1.Image }}
```

## Runtime Event

Runtime 应发布：

- FlowStarted
- FlowStopped
- TokenCreated
- NodeWaiting
- NodeStarted
- NodeCompleted
- NodeFailed
- NodeTimeout
- OutputProduced
- ImageProduced
- QueueWarning

## 线程规则

- 不阻塞 UI 线程。
- 不在相机回调线程执行重算法。
- 设备操作使用 `CancellationToken`。
- 高吞吐任务使用有界队列。
- 相机回调只做轻量入队和事件转发。

## 第一版范围

第一版可以只支持：

- 线性执行。
- `Next` / `Error` 输出端口。
- Manual entry trigger。
- RuntimeEvent。
- 简单变量池。
- 基础 NodeFactory。

后续再扩展：

- 并行分支。
- AND Join。
- FrameGroup。
- ScanGroup。
- 断点和单步调试。
## 2026-06 Runtime Model

- `RuntimeFlowPlan` is the execution index for `FlowRunner`. It preserves output-port edge order and enables one node output to fan out to multiple downstream nodes.
- `FlowRunner` keeps existing runtime events and variable-pool behavior, but cycle detection now follows the active execution path instead of a global visited set.
- `DefaultCameraFrameRouter` subscribes to registered cameras, buffers lightweight frame notifications, and supports `WaitNextFrame`, `Any`, `TriggerId`, `ScanGroupId`, and basic stream collection modes.
- `FlowTaskQueue` provides bounded queue execution with capacity, max degree of parallelism, wait/reject full modes, and queue runtime events.
- `FlowExecutionContext` carries optional `Devices`, `CameraFrames`, and `Queues` services so nodes remain UI-independent and adapter-driven.
- `IVisionImage` references should be cloned when work crosses async boundaries or queues; queued save nodes snapshot image references before background execution.
