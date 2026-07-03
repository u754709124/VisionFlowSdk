# 03 - Runtime Design

## 目标

Runtime 必须能在没有 WPF Designer 的生产进程中加载 `.flowruntime` 并执行。

## 核心类型

```text
Vision.Flow.Core.Runtime.Engine.FlowEngine
Vision.Flow.Core.Runtime.Engine.FlowRunner
Vision.Flow.Core.Runtime.Execution.FlowExecutionContext
Vision.Flow.Core.Runtime.Execution.FlowListenerContext
Vision.Flow.Core.Runtime.State.FlowToken
Vision.Flow.Core.Runtime.State.VariablePool
Vision.Flow.Core.Contracts.Nodes.NodeRegistry
Vision.Flow.Core.Runtime.Events.FlowRuntimeEvent
Vision.Flow.Core.Runtime.Engine.RuntimeFlowPlan
```

## 执行模型

```text
StartAsync()
  -> start listener nodes, such as camera.hard_trigger
TriggerAsync(entryName, token)
  -> find Entry.TargetNodeId
  -> execute node
  -> write outputs to VariablePool
  -> publish FlowRuntimeEvent
  -> route by OutputPort
StopAsync()
  -> stop listener nodes
  -> publish FlowStopped
```

硬触发相机节点通过 `IFlowListenerNode` 在 `StartAsync` 阶段订阅 `ICameraAdapter.FrameArrived`。相机回调线程只做轻量帧克隆和后台投递，后续节点由 `FlowRunner.DispatchAsync` 在后台任务中调度。

## Core 内置节点

Core 内置节点包括基础流程节点和通用相机节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
camera.soft_trigger
camera.hard_trigger
camera.parameter.set
```

`camera.soft_trigger` 调用 `ICameraAdapter.GrabOneAsync` 获取单帧。`camera.hard_trigger` 订阅相机硬触发回调。`camera.parameter.set` 设置一个可写相机参数。三者都只依赖 Core Adapter 契约，不引用具体相机 SDK。

## Runtime 服务

`FlowExecutionContext` 携带：

- `IDeviceRegistry`
- `IFlowContinuationDispatcher`
- `IFlowEventSink`

`FlowListenerContext` 携带监听节点启动所需的 `IDeviceRegistry`、`IFlowContinuationDispatcher` 和 `IFlowEventSink`，不承载单次 Token 变量状态。

## 线程规则

- 节点实现使用 `async` / `await` 和 `CancellationToken`。
- 相机回调线程只做轻量封装和转发，不执行后续节点。
- 重算法、保存、数据库等长耗时工作由具体项目节点自行放入后台任务或有界队列。
- 生产运行不依赖 Designer UI。
