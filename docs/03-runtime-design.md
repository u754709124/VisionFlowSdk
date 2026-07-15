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
  -> start listener nodes, such as project camera hard-trigger nodes
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

项目专属硬触发相机节点通过 `IFlowListenerNode` 在 `StartAsync` 阶段订阅 `ICameraAdapter.FrameArrived`。相机回调线程只做轻量帧克隆和后台投递，后续节点由 `FlowRunner.DispatchAsync` 在后台任务中调度。

## Core 内置节点

Core 内置节点只包括基础流程节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

相机软触发、硬触发和参数设置等设备节点应放在项目专属节点库中，并只通过 Core Adapter 契约访问设备，不引用具体相机 SDK。

## Runtime 服务

`FlowExecutionContext` 携带：

- `IDeviceRegistry`
- `IFlowContinuationDispatcher`
- `IFlowEventSink`
- `ISettingValueResolver`

节点在 `ExecuteAsync` 中通过 `GetSettingValue(name)` 或 `GetSettingValue<T>(name)` 获取配置。常量模式直接返回 `ConstantValue`；变量模式由 `ISettingValueResolver` 根据结构化 `VariableSelector` 从上游节点输出变量池或当前 Token 解析。运行时不再提供 `GetInputValue`，控制端口不参与配置取值。

`FlowListenerContext` 携带监听节点启动所需的 `IDeviceRegistry`、`IFlowContinuationDispatcher` 和 `IFlowEventSink`，不承载单次 Token 变量状态。

## 线程规则

- 节点实现使用 `async` / `await` 和 `CancellationToken`。
- 相机回调线程只做轻量封装和转发，不执行后续节点。
- 重算法、保存、数据库等长耗时工作由具体项目节点自行放入后台任务或有界队列。
- 生产运行不依赖 Designer UI。
