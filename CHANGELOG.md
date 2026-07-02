# CHANGELOG

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
