# 03 - Runtime Design

## 目标

Runtime 必须能在没有 WPF Designer 的生产进程中加载 `.flowruntime` 并执行。

## 核心类型

```text
Vision.Flow.Core.Runtime.Engine.FlowEngine
Vision.Flow.Core.Runtime.Engine.FlowRunner
Vision.Flow.Core.Runtime.Execution.FlowExecutionContext
Vision.Flow.Core.Runtime.Execution.FlowListenerContext
Vision.Flow.Core.Runtime.Execution.FlowTriggerRequest
Vision.Flow.Core.Runtime.Execution.FlowRunResult
Vision.Flow.Core.Runtime.State.FlowToken
Vision.Flow.Core.Runtime.State.VariablePool
Vision.Flow.Core.Contracts.Nodes.NodeRegistry
Vision.Flow.Core.Runtime.Events.FlowRuntimeEvent
Vision.Flow.Core.Runtime.Engine.RuntimeFlowPlan
```

## 执行模型

```text
StartAsync()
  -> start only listener nodes referenced by NodeEvent entries
TriggerAsync(FlowTriggerRequest)
  -> match request Source with Entry.TriggerKind
  -> validate and normalize declared TriggerInputs
  -> enter the bounded per-entry execution gate
  -> Manual / External: execute Entry.TargetNodeId
  -> NodeEvent: write listener outputs and route SourceNodeId outgoing edges
  -> execute node
  -> write outputs to VariablePool
  -> route by OutputPort
  -> return FlowRunResult with final variable snapshot
StopAsync()
  -> stop listener nodes
  -> publish FlowStopped
```

项目专属硬触发相机节点通过 `IFlowListenerNode` 在 `StartAsync` 阶段订阅 `ICameraAdapter.FrameArrived`。只有被 `NodeEvent` 入口的 `SourceNodeId` 引用的监听节点会启动。相机回调线程只做轻量帧克隆和续流投递，`BoundFlowContinuationDispatcher` 会补齐入口名、FlowRunId、Token、变量池和 TriggerInputs，后续节点由统一入口运行路径调度。

## 统一触发与生命周期

手动按钮、外部宿主和监听节点最终都进入同一入口运行路径。手动/外部调用示例：

```csharp
var result = await runner.TriggerAsync(new FlowTriggerRequest
{
    EntryName = "ExternalInspection",
    Source = FlowTriggerSource.External,
    Token = token,
    Inputs = new Dictionary<string, object>
    {
        { "inspectionResult", "OK" }
    }
});
```

`FlowRunResult.Status` 为 `Succeeded`、`Failed`、`Cancelled` 或 `Rejected`。成功、失败和取消结果携带终态 `VariablePool.Snapshot()`；入口不匹配、输入无效或队列满导致的拒绝保持空变量字典。常规运行终态通过结果返回，不依赖异常推断。

每次请求先发布 `TokenCreated`，进入执行槽后发布 `FlowRunStarted`，最后发布 `FlowRunCompleted`、`FlowRunFailed`、`FlowRunCancelled` 或 `FlowRunRejected`。同一次运行的事件共享 `FlowRunId`；Started 事件状态为 `Running`。生命周期事件只携带入口名、触发来源和有效 TriggerInputs，不复制全量变量池，避免图像等大对象放大事件负载。

## 入口并发与图内并行

入口策略和图内扇出是两个独立层次：

- `TriggerExecutionPolicy.MaxConcurrentRuns` 控制同一入口同时运行多少个 FlowRun。
- `QueueCapacity` 控制等待执行槽的请求数；满时按当前唯一策略 `Reject` 返回。
- 默认 `MaxConcurrentRuns = 1`，因此同一入口串行；设为 2 或更高可允许多个运行并行。
- 节点执行后的多条出边仍由 `FlowExecutionOptions.FanOutMode` 决定串行或并行，不由入口策略改变。

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
- `TriggerInputs`

节点在 `ExecuteAsync` 中通过 `GetSettingValue(name)` 或 `GetSettingValue<T>(name)` 获取配置。常量模式直接返回 `ConstantValue`；变量模式由 `ISettingValueResolver` 根据结构化 `VariableSelector` 从上游节点输出变量池、入口 TriggerInputs 或当前 Token 解析。运行时不再提供 `GetInputValue`，控制端口不参与配置取值。

`FlowListenerContext` 携带监听节点启动所需的入口定义、`IDeviceRegistry`、`IFlowContinuationDispatcher` 和 `IFlowEventSink`，不承载单次 Token 变量状态。

## 线程规则

- 节点实现使用 `async` / `await` 和 `CancellationToken`。
- 相机回调线程只做轻量封装和转发，不执行后续节点。
- 重算法、保存、数据库等长耗时工作由具体项目节点自行放入后台任务或有界队列。
- 生产运行不依赖 Designer UI。
