# 07 - Test Plan

## 测试项目

```text
tests/Vision.Flow.Tests
```

## 覆盖范围

- Runtime：线性执行、扇出、并行扇出、重入汇合、环检测、缺失入口、错误路由、超时路由、节点重试与失败恢复、Continuation 调度，以及已移除公共面的守卫。
- Unified Trigger：Manual / External 来源匹配、入口输入校验与类型转换、TriggerInput 配置解析、结果变量快照、生命周期事件和 FlowRunId、默认串行、配置并行、队列满拒绝。
- NodeEvent：只启动入口引用的 `IFlowListenerNode`、监听续流携带入口与 TriggerInputs、按 SourceNodeId 出边继续、停止时释放监听器。
- Serialization / Publish：Schema v2 round-trip、明确拒绝 v1、入口类型/输入/执行策略 round-trip、节点 `ExecutionPolicy` 完整协议与缺失/null 默认值、结构化 Setting Selector、TriggerInput 可达性和类型冲突、发布后移除 view state、样例流程校验。
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
- `StopFlow` 使 FlowRun 失败；`ErrorBranch` 沿失败端口或 `Error` 端口继续；`DefaultOutputs` 写出声明值后从 `Next` 继续。
- `NodeStarted`、`NodeRetrying`、`NodeFailed`、`NodeTimeout`、`NodeRecovered` 与 `NodeCancelled` 的事件顺序，以及 `Attempt` 从 1 开始、`FailureKind` 和 `FailureStrategy` 的数据语义。

序列化与发布测试至少覆盖：

- 生产 JSON 始终包含 `ExecutionPolicy`、嵌套 `RetryPolicy`、`FailureStrategy` 与 `DefaultOutputs`，非默认值可以完成设计态和运行态 round-trip。
- v2 JSON 缺失或显式写入 `null` 的节点策略时回落默认值；`DefaultOutputs` 反序列化及发布克隆后仍按大小写不敏感查找。
- 非法超时、并发数、重试次数或重试间隔产生 `NodeExecutionPolicyInvalid`。
- `ErrorBranch` 缺少 `Error` 控制输出端口时产生 `NodeErrorPortMissing`。
- `DefaultOutputs` 缺键、多键或值类型不兼容时产生 `NodeDefaultOutputInvalid`。

## 不再覆盖

SDK 测试不再覆盖 light/motion/recipe/save/database/group/scan/stitch/fusion 节点，也不再覆盖已移除的相机帧路由实现。具体项目实现这些能力时，应在项目自己的测试集中覆盖。

## 命令

```powershell
./build/build.ps1
./build/test.ps1
```
