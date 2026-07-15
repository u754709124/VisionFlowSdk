# 07 - Test Plan

## 测试项目

```text
tests/Vision.Flow.Tests
```

## 覆盖范围

- Runtime：线性执行、就绪队列、串行/并行扇出、单次 fan-in 汇聚、条件分支 skip 传播、环检测、缺失入口、错误路由、超时路由、节点重试与失败恢复、Continuation 来源约束，以及已移除公共面的守卫。
- Unified Trigger：Manual / External 来源匹配、入口输入校验与类型转换、TriggerInput 配置解析、结果变量快照、生命周期事件和 FlowRunId、默认串行、配置并行、队列满拒绝。
- NodeEvent：只启动入口引用的 `IFlowListenerNode`、监听续流携带入口与 TriggerInputs、按 SourceNodeId 出边继续、停止时释放监听器。
- Serialization / Publish：Schema v2 round-trip、明确拒绝 v1、入口类型/输入/执行策略 round-trip、节点 `ExecutionPolicy` 完整协议与缺失/null 默认值、结构化 Setting Selector、TriggerInput 可达性和类型冲突、入口绕过变量来源警告、发布后移除 view state、校验通过后写入 `.flowruntime`、无效发布不覆盖、样例流程校验。
- Core 节点：注册、日志事件、延时、变量写入、AND Join、Condition 分支。
- Core 契约：`VisionImageReference` 生命周期和精简公共面的守卫。
- Designer：入口触发配置、配置项固定值/变量切换与常量保留、祖先与入口变量候选范围、类型过滤、失效 Selector 保留、属性面板只读、节点库交互、拖拽、停止调试、按钮状态恢复、节点卡片运行状态显示。
- Variable Settings：常量/变量切换、上游来源限制、类型兼容、TriggerInput、Token 与对象子路径解析、JSON 中不出现 `InputBindings`。
- Demo：解决方案构建覆盖 WinForms Demo 和 Designer WPF Demo。

## 节点执行策略验收

Runtime 测试至少覆盖：

- `RetryPolicy.Enabled = false` 时只执行一次；启用后总尝试次数不超过 `MaxRetries + 1`。
- `Execution` 和 `Timeout` 失败按固定间隔重试，并能在后续尝试成功时发布 `NodeRecovered`。
- `Binding`、`Configuration` 和 `Cancelled` 不重试。
- `TimeoutMs = 0` 继承全局超时，正数覆盖全局超时。
- 重试等待期间取消时不再启动下一次尝试，发布 `NodeCancelled`，FlowRun 终态为 `Cancelled`。
- 超时尝试即使延迟响应取消，也必须退出后才开始重试，确保 `MaxConcurrentExecutions` 不被旧尝试穿透。
- `StopFlow` 使 FlowRun 失败；`ErrorBranch` 沿失败端口或 `Error` 端口继续；`DefaultOutputs` 写出声明值后从 `Next` 继续。
- `NodeStarted`、`NodeRetrying`、`NodeFailed`、`NodeTimeout`、`NodeRecovered` 与 `NodeCancelled` 的事件顺序，以及 `Attempt` 从 1 开始、`FailureKind` 和 `FailureStrategy` 的数据语义。

## 就绪队列调度验收

Runtime 测试至少覆盖：

- `FanOutMode.Sequential` 按出边定义顺序处理同批就绪节点，每个节点完成后再取下一个节点。
- `FanOutMode.Parallel` 在 `MaxDegreeOfParallelism >= 2` 时允许两个兄弟分支真实重叠执行。
- 并行分支发生 `StopFlow` 失败时，Runtime 取消并等待协作式兄弟分支退出。
- 多分支 fan-in 只有在所有入边都由 `Unknown` 解析为 `Taken` 或 `Skipped` 后才就绪，并且每次激活只执行一次。
- 条件节点未选中的输出端口标记为 `Skipped`；skip 可以穿过未执行节点继续传播，使下游汇聚不被永久阻塞。
- 所有入边均为 `Skipped` 的节点不执行、不发布 `NodeStarted`，其出边继续传播 `Skipped`。
- Manual / External 入口可以从流程中间节点直接开始，不执行该入口上游节点。
- NodeEvent continuation 从监听源的输出端口进入同一套就绪队列；其 fan-out/fan-in 结果与手动入口一致。
- 串行与并行模式都满足“所有入边已解析且至少一条入边 Taken”的就绪条件，不因完成先后产生重复执行。
- `NodeSkipped` 对每个被跳过节点只发布一次；Continuation 环检测先于源输出和完成事件副作用。

序列化与发布测试至少覆盖：

- 生产 JSON 始终包含 `ExecutionPolicy`、嵌套 `RetryPolicy`、`FailureStrategy` 与 `DefaultOutputs`，非默认值可以完成设计态和运行态 round-trip。
- v2 JSON 缺失或显式写入 `null` 的节点策略时回落默认值；`DefaultOutputs` 反序列化及发布克隆后仍按大小写不敏感查找。
- 非法超时、并发数、重试次数或重试间隔产生 `NodeExecutionPolicyInvalid`。
- `ErrorBranch` 缺少 `Error` 控制输出端口时产生 `NodeErrorPortMissing`。
- `DefaultOutputs` 缺键、多键或值类型不兼容时产生 `NodeDefaultOutputInvalid`。

## 发布期有向环校验

- 发布校验对节点间的有效控制流连线执行有向环检测，覆盖多节点闭环和节点自环。
- 检测到环时产生稳定错误码 `FlowCycleDetected`，并在问题中携带闭环节点、连线序号及 `Edges[n]` 字段路径，便于设计器定位。
- 空连线、缺少端点或引用不存在节点的连线由既有结构校验报告，不参与环检测，避免产生误导性的重复诊断。
- 含环流程在发布前即判定无效，不依赖运行时路径恰好进入该环后才失败。

## 不再覆盖

SDK 测试不再覆盖 light/motion/recipe/save/database/group/scan/stitch/fusion 节点，也不再覆盖已移除的相机帧路由实现。具体项目实现这些能力时，应在项目自己的测试集中覆盖。

## 命令

```powershell
./build/build.ps1
./build/test.ps1
```
