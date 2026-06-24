# 15 - Bug 修复提示词模板

请修复以下问题：

问题描述：

[在这里粘贴错误、异常、测试失败、UI异常或日志]

上下文：

- 项目：VisionFlowSdk
- 相关模块：[Core / Nodes / DeviceAdapters / Designer.Wpf / Demo.WinForms / Tests]
- 期望行为：[描述期望]
- 当前行为：[描述当前错误]

修复要求：

1. 先定位根因，不要直接大面积重构。
2. 优先做最小、安全、可测试的修复。
3. 不破坏 AGENTS.md 中的依赖规则。
4. 如果涉及 Runtime，增加或更新测试。
5. 如果涉及节点，增加或更新节点测试。
6. 如果涉及 WPF UI，确认 Demo.DesignerWpf 可运行。
7. 修复后运行相关测试或 build/test.ps1。

输出：

- 根因分析。
- 修改文件列表。
- 测试结果。
- 是否还有遗留风险。
