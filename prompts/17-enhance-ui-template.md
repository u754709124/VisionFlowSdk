# 17 - UI 增强提示词模板

请增强 WPF 设计器 UI：

增强目标：

[例如：节点连线更美观、属性面板更好用、变量选择器支持类型过滤]

相关项目：

- src/Vision.Flow.Designer.Wpf

必须遵守：

1. 不修改 Flow.Core 的运行逻辑，除非确实需要增加纯数据 descriptor。
2. 不让 WPF 类型进入 `.flowruntime`。
3. 不让 Designer ViewModel 进入 Runtime。
4. UI 状态只能保存到 `.flowdesign` 的 view state。
5. 生产 Demo.WinForms 不应受影响。

具体需求：

[列出 UI 行为]

验收条件：

1. Demo.DesignerWpf 可运行。
2. 保存/加载 `.flowdesign` 正常。
3. 发布 `.flowruntime` 不包含 UI 状态。
4. build/test.ps1 通过。
