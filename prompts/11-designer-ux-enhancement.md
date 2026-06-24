# 11 - WPF 操作体验增强

请实现阶段 11：WPF 设计器操作体验增强。

请先阅读：

- docs/06-designer-ui-guide.md

增强目标：

让流程设计器接近现代节点编排工具的使用体验。

请实现：

## 画布

1. 鼠标滚轮缩放。
2. 鼠标中键拖动画布。
3. 空格 + 左键拖动画布。
4. 网格背景。
5. 节点拖拽时吸附到网格。
6. 画布坐标和缩放状态保存到 `.flowdesign`。

## 连线

1. 从输出端口拖到输入端口创建连线。
2. 连线使用贝塞尔曲线。
3. 连线过程中实时预览。
4. 不允许非法端口连接。
5. 支持删除连线。

## 节点

1. 运行状态样式：
   Idle、Waiting、Running、Success、Failed、Timeout、Disabled。
2. 节点显示关键 settings 摘要。
3. 双击节点可以重命名。
4. 右键菜单：删除、复制、禁用。

## 属性面板

1. 根据 NodeSettingDescriptor 动态选择编辑器。
2. 支持 TextBox、NumberBox、ComboBox、CheckBox。
3. 支持 CameraSelector、RecipeSelector 的占位实现。
4. 支持 VariableSelectorControl。

## 调试

1. RuntimeEvent 日志面板。
2. 点击日志定位节点。
3. 节点显示最后一次耗时。
4. 错误节点显示错误信息 tooltip。

验收条件：

1. Demo.DesignerWpf 操作流畅。
2. 保存/加载后节点位置和连线正确。
3. 发布 `.flowruntime` 不包含 view state。
4. build/test.ps1 通过。
