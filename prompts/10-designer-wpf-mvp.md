# 10 - WPF 设计器 MVP

请实现阶段 10：Vision.Flow.Designer.Wpf MVP。

请先阅读：

- docs/06-designer-ui-guide.md

目标：

实现可用但不过度复杂的 WPF 流程设计器。

需要实现：

Controls:

- FlowDesignerControl
- NodeCardControl
- PortControl
- EdgeLayerControl
- PropertyPanelControl
- NodePaletteControl
- RuntimeDebugPanelControl

ViewModels:

- FlowDesignerViewModel
- NodeViewModel
- PortViewModel
- EdgeViewModel
- PropertyPanelViewModel
- NodePaletteViewModel
- RuntimeDebugViewModel

功能：

1. 左侧显示节点库，节点来自 NodeDescriptor。
2. 中间 Canvas 显示节点卡片。
3. 支持点击左侧节点添加到画布。
4. 支持拖动节点。
5. 支持选择节点。
6. 右侧属性面板显示并编辑节点 Settings。
7. 支持保存 `.flowdesign`。
8. 支持发布 `.flowruntime`。
9. 支持调试运行当前流程。
10. 调试运行时根据 RuntimeEvent 高亮节点。

第一版可以简化：

- 连线可以先用按钮或简单交互实现。
- 不要求自动布局。
- 不要求小地图。
- 不要求撤销重做。

UI 要求：

- 简洁、美观。
- 节点卡片有标题、图标占位、关键参数摘要、端口。
- 有浅色主题 ResourceDictionary。
- 代码结构清晰，方便后续增强。

验收条件：

1. Demo.DesignerWpf 能打开 FlowDesignerControl。
2. 能添加节点、编辑属性、保存 `.flowdesign`。
3. 能发布 `.flowruntime`。
4. 能使用 FakeAdapters 调试运行。
5. build/test.ps1 通过。
