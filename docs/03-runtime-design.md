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

## 就绪队列与控制流边状态

每次 FlowRun 都创建独立的调度状态，控制流边在本次运行内依次从 `Unknown` 解析为 `Taken` 或 `Skipped`：

- 节点执行完成后，命中返回 `OutputPort` 的出边标记为 `Taken`，该节点其余出边标记为 `Skipped`。
- 普通节点只有在**所有入边都已解析**，并且**至少一条入边为 `Taken`** 时才进入就绪队列。
- 所有入边都为 `Skipped` 时，节点本身不执行；调度器把它的全部出边继续标记为 `Skipped`，直到下游状态稳定。
- 多条有效分支重新汇聚到同一节点时，汇聚节点等待全部入边解析后只入队一次，不按每条递归路径重复执行。
- Manual / External 的 `Entry.TargetNodeId` 作为本次运行的直接激活点，即使流程文件中还存在来自入口上游的连线，也不回溯执行上游节点。
- NodeEvent 续流把监听源节点的本次 `OutputPort` 当作已经完成的源输出，再使用同一套边状态和就绪队列调度下游。
- Continuation 的 `SourceNodeId` 由当前节点或监听入口上下文绑定；缺失时自动补齐，伪造其他来源会立即失败，避免重入同一节点并发闸门。

`FanOutMode.Sequential` 使用一个确定性的就绪队列，按边定义顺序处理同批就绪节点。`FanOutMode.Parallel` 允许多个就绪节点同时执行，并受 `MaxDegreeOfParallelism` 以及各节点 `MaxConcurrentExecutions` 约束；无论采用哪种模式，入边解析规则和“单次激活只执行一次”的语义保持一致。

并行分支共享同一个 `FlowToken` 与线程安全变量池；Token 的 `Values` / `Metadata` 使用线程安全映射。节点失败策略是唯一分支失败协议：`StopFlow` 会取消并等待仍在运行的协作式兄弟节点，`ErrorBranch` / `DefaultOutputs` 则把失败转换为可继续的节点结果。Runtime 不再提供无实际语义的分支 Token 克隆或“忽略分支失败”开关。进程内节点无法被强制终止，扩展节点必须响应传入的取消令牌，否则超时、停止或并行失败收敛会继续等待该节点退出。

该汇聚规则解决的是单次 DAG 调度中的控制流收敛，不替代 `join.and` 的跨事件、跨到达批次和 `JoinKey` 聚合语义。流程发布仍应拒绝控制流环，运行时就绪队列不依赖递归路径的 visited 集合来掩盖非法环。

## 节点执行策略

每个 `NodeDefinition` 都携带独立的 `NodeExecutionPolicy`。入口执行策略控制整个 FlowRun 的并发与排队，节点执行策略则由统一节点包装器在每次节点调用时应用：

- `TimeoutMs = 0`：继承 `FlowExecutionOptions.DefaultNodeTimeoutMs`；正数覆盖全局值。
- `MaxConcurrentExecutions = 1`：稳定协议默认值。Runtime 按 `NodeId` 建立并发闸门；闸门覆盖完整执行、超时收敛、重试等待、失败恢复和输出写入，同一 Runner 的多个 FlowRun 也共享该限制。
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

节点超时通过异步竞争实现。Runtime 会取消本次尝试使用的关联 `CancellationToken`，把结果归类为 `Timeout`，并等待该次尝试退出后才允许重试或释放节点并发闸门，避免旧尝试与新尝试重叠。节点实现仍必须及时响应取消令牌；忽略取消会使超时收敛继续等待。FlowRun 被宿主取消或在重试等待期间被取消时，不再执行下一次尝试，发布 `NodeCancelled`，最终返回 `FlowRunStatus.Cancelled`。

结构化变量选择器仍展示全部控制流祖先输出。发布校验会额外分析每个可达入口：若某个中间入口或旁路能到达目标节点却绕过变量来源节点，则产生 `VariableSourceNotGuaranteed` 警告；TriggerInput 未被每个可达入口声明时产生 `TriggerInputNotGuaranteed`。这类流程仍可发布，但对应路径运行时可能产生 `Binding` 失败，应通过拓扑调整或节点失败策略显式处理。

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
