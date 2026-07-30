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
- Designer：主题资源解析、内部/外置命令栏、入口触发配置、配置项固定值/变量切换与常量保留、实例级动态 Descriptor 刷新与字段调和、祖先与入口变量候选范围、类型过滤、失效 Selector 保留、属性草稿应用/重置/校验/三种未保存决策、只读模式、节点库四字段搜索与折叠恢复、拖拽、卡片外侧短条端口锚点、无三角箭头的贝塞尔连线末端、缩略图视野换算与边界限制、DPI 像素对齐平移、调试抽屉三态和失败/超时自动展开、停止调试、按钮状态恢复、节点卡片运行状态显示。
- Variable Settings：常量/变量切换、上游来源限制、类型兼容、TriggerInput、Token、环境变量默认值与运行覆盖、对象子路径解析、JSON 中不出现 `InputBindings`。
- Demo：解决方案构建覆盖 WinForms Demo 和 Designer WPF Demo。

## Designer 现代界面验收

STA 交互测试至少覆盖：

- `FlowDesignerTheme.CreateModern()` 可以解析语义色、40 px 字段、主按钮、折叠器和滚动条；主按钮悬停模板保持绿色。
- `ToolbarPlacement=Internal` 时命令栏属于设计器；`External` 时 `ToolbarView` 无父元素、资源自包含，并在 Arrange 到 300 px 后所有可见按钮都位于边界内。
- 节点库分别匹配中文名称、描述、`NodeType` 和 `Category`；搜索时匹配组展开，清空后恢复搜索前折叠状态，纯空白查询等同于清空。
- 输入、输出端口短条分别位于卡片左右描边外侧，锚点位于短条中心；贝塞尔连线直接结束于输入端口锚点，不绘制额外三角箭头。
- 节点阴影与文字内容分层渲染，卡片内容层不挂载位图效果；75% 缩放使用 `Display`、ClearType 和固定像素提示，离开该范围后恢复 `Ideal` 字形度量。
- 同一合成帧内的高频平移只保留最新偏移，滚轮缩放累积为单一目标且不会逐事件同步重排；帧执行后不残留待处理交互状态，独立阴影层启用位图缓存。
- 调试抽屉 `Auto` 在编辑/调试模式间自动收起和展开；用户 `Open` / `Closed` 偏好跨模式保留；失败和超时事件强制即时展开。
- 节点属性修改不直接写入源节点；一次应用同时提交名称、设置、重试和默认输出，重置恢复最近应用基线。
- 非法数字、必填、动态候选失效、变量缺失/类型不兼容和执行策略范围错误禁止应用、保留 dirty 草稿，并定位到稳定 Tag 的首个错误控件。
- 普通固定值生成手工输入 `TextBox`；只有宿主为具体 Descriptor 返回非 `null` 选项数据源时生成不可自由输入的现代 `ComboBox`，空数据源仍保持空下拉框。
- 普通字段输入超长文本后仍保持 `NoWrap`、40 px 高度及后续表单坐标；既有 `Mappings` / `Channels` 字段继续使用可回车、可换行的多行编辑器。
- 固定值/变量分段按钮的实际 Arrange 边界保留明确间距；两个按钮、固定值文本框、变量选择器和宿主候选下拉框均精确保持 40 px 高度及相同顶边。变量选择器解析专用自绘样式，文本框、下拉框、候选行和箭头均不使用系统原生模板。
- 属性值、变量状态和执行策略使用固定错误槽；分别记录错误出现/消失前后的编辑器与后续表单项坐标，验证边界完全不变且错误红色优先于焦点、悬停状态。
- `RefreshSelectedNodeProperties()` 保留设置与执行策略的非法原始文本和错误；动态候选失效不清空原值。
- `AffectsDescriptor` 固定值切换后立即显示新实例 Setting/Output；旧专属 Setting 和回退输出被移除，新字段按类型补默认值，共同字段与无关非法原始文本保留。
- 实例 Descriptor 变化后节点卡片和上游变量候选同步更新；引用已移除输出的下游 Selector 保留原协议值并由校验器报告。
- 切换节点、加载文档和进入调试分别验证 Apply / Discard / Cancel；调试只读模式隐藏应用动作。

WPF Demo 的解决方案构建还应验证无边框深色标题栏 XAML，包括拖动、双击最大化、最小化、最大化/还原、关闭、缩放边框和关闭前草稿处理事件。

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
