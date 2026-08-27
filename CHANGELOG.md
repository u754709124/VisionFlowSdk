# CHANGELOG

## Unreleased

- 将 `condition.if` 升级到 v3 强类型比较协议：左值必须绑定变量，右值支持固定值或变量。
- 数值类型支持六种顺序/相等比较，字符串和枚举支持相等与不相等；枚举固定值由绑定输出的 `EnumType` 生成下拉项。
- Designer 与发布校验器共享变量类型约束，失效配置保留原值并显示错误。

## 0.2.0 - Core Node Consolidation

- 将 SDK 内置节点收缩为 Core 基础流程节点。
- 移除独立 Nodes、DeviceAdapters 项目及内置设备/算法/保存/拼图/融合节点。
- 外部引用收口为 `Vision.Flow.Core` 与 `Vision.Flow.Designer.Wpf`。
- 更新 Demo、样例流程、测试和文档以匹配项目专属节点扩展模式。

## 0.1.0 - Initial Planning

- 初始化 VisionFlowSdk 文档包。
- 定义解决方案结构。
- 定义设计态 `.flowdesign` 与运行态 `.flowruntime`。
- 定义 Core / Nodes / DeviceAdapters / Designer.Wpf / Tests / Demos 职责。
- 定义 Codex 分阶段开发提示词。
