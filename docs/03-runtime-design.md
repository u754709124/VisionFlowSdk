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

## 节点执行策略

每个 `NodeDefinition` 都携带独立的 `NodeExecutionPolicy`。入口执行策略控制整个 FlowRun 的并发与排队，节点执行策略则由统一节点包装器在每次节点调用时应用：

- `TimeoutMs = 0`：继承 `FlowExecutionOptions.DefaultNodeTimeoutMs`；正数覆盖全局值。
- `MaxConcurrentExecutions = 1`：稳定协议默认值。当前阶段只完成模型、序列化和校验，节点级并发门由后续调度器实现。
- `RetryPolicy.Enabled = false`：只执行首次尝试；`MaxRetries` 不参与计算。
- `RetryPolicy.MaxRetries = 3`：启用重试后最多追加 3 次尝试，不包含首次执行。
- `RetryPolicy.RetryIntervalMs = 1000`：两次尝试之间使用可取消的固定等待时间。
- `FailureStrategy = StopFlow`：重试耗尽或遇到不可重试失败后的默认行为。
- `DefaultOutputs`：`DefaultOutputs` 策略使用的输出字典，键按大小写不敏感比较。

`MaxRetries` 表示“重试次数”而不是“总尝试次数”，因此最大总尝试次数为 `MaxRetries + 1`。只有 `Execution` 和 `Timeout` 失败可以重试；`Binding`、`Configuration` 与 `Cancelled` 不重试。

统一执行包装流程如下：

```text
Attempt 1 -> NodeStarted -> execute with timeout
  success -> write outputs -> NodeCompleted
  Execution / Timeout and retries remain
          -> NodeRetrying -> cancellable fixed delay -> next Attempt
  non-retryable or retries exhausted
          -> NodeFailed / NodeTimeout -> apply FailureStrategy
```

节点超时通过异步竞争实现。Runtime 会取消本次尝试使用的关联 `CancellationToken`，并把结果归类为 `Timeout`；节点实现仍必须及时响应取消令牌。FlowRun 被宿主取消或在重试等待期间被取消时，不再执行下一次尝试，发布 `NodeCancelled`，最终返回 `FlowRunStatus.Cancelled`。

## 失败分类与恢复

`NodeFailureKind` 是稳定的运行分类：

- `None`：没有失败。
- `Binding`：变量 Selector 缺失、无法解析或变量值无法转换到配置类型。
- `Configuration`：Setting 结构、常量值或节点配置无效。
- `Execution`：节点返回失败、返回空任务/结果或抛出普通执行异常。
- `Timeout`：单次尝试超过有效超时时间。
- `Cancelled`：宿主取消、停止运行或重试等待被取消。

重试成功后发布 `NodeRecovered`，随后按正常成功路径写出变量并发布 `NodeCompleted`。最终失败按 `FailureStrategy` 处理：

- `StopFlow`：停止当前 FlowRun，不再调度下游，FlowRun 终态为 `Failed`。
- `ErrorBranch`：沿失败结果的 `OutputPort` 继续；端口为空时使用 `Error`。发布时节点 Descriptor 必须声明 `Error` 控制输出端口，运行时还必须存在对应出边，否则 FlowRun 仍为 `Failed`。
- `DefaultOutputs`：把策略中配置的全部默认输出写入变量池，并从 `Next` 端口继续；该次恢复可以使 FlowRun 最终成功。

节点尝试事件的 `Data` 使用 `Attempt`、`FailureKind` 和 `FailureStrategy` 描述上下文。`Attempt` 从 1 开始；`NodeRetrying` 中记录的是即将执行的下一次尝试。`NodeRecovered` 在重试成功时以 `Retry` 标识恢复方式，在失败策略恢复时以 `ErrorBranch` 或 `DefaultOutputs` 标识恢复方式。`NodeFailed`、`NodeTimeout`、`NodeCancelled` 和 `NodeRecovered` 可用于日志、Designer 状态与生产诊断，不应由节点自行重复发布。

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
