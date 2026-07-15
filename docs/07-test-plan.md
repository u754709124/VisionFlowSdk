# 07 - Test Plan

## 测试项目

```text
tests/Vision.Flow.Tests
```

## 覆盖范围

- Runtime：线性执行、扇出、并行扇出、重入汇合、环检测、缺失入口、错误路由、超时路由、Continuation 调度，以及已移除公共面的守卫。
- Unified Trigger：Manual / External 来源匹配、入口输入校验与类型转换、TriggerInput 配置解析、结果变量快照、生命周期事件和 FlowRunId、默认串行、配置并行、队列满拒绝。
- NodeEvent：只启动入口引用的 `IFlowListenerNode`、监听续流携带入口与 TriggerInputs、按 SourceNodeId 出边继续、停止时释放监听器。
- Serialization / Publish：Schema v2 round-trip、明确拒绝 v1、入口类型/输入/执行策略 round-trip、结构化 Setting Selector、TriggerInput 可达性和类型冲突、发布后移除 view state、样例流程校验。
- Core 节点：注册、日志事件、延时、变量写入、AND Join、Condition 分支。
- Core 契约：`VisionImageReference` 生命周期和精简公共面的守卫。
- Designer：入口触发配置、配置项固定值/变量切换与常量保留、祖先与入口变量候选范围、类型过滤、失效 Selector 保留、属性面板只读、节点库交互、拖拽、停止调试、按钮状态恢复、节点卡片运行状态显示。
- Variable Settings：常量/变量切换、上游来源限制、类型兼容、TriggerInput、Token 与对象子路径解析、JSON 中不出现 `InputBindings`。
- Demo：解决方案构建覆盖 WinForms Demo 和 Designer WPF Demo。

## 不再覆盖

SDK 测试不再覆盖 light/motion/recipe/save/database/group/scan/stitch/fusion 节点，也不再覆盖已移除的相机帧路由实现。具体项目实现这些能力时，应在项目自己的测试集中覆盖。

## 命令

```powershell
./build/build.ps1
./build/test.ps1
```
