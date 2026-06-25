# 06 - Designer UI Guide

## UI 目标

WPF Designer 提供类似现代节点编排工具的体验。

## 主界面

```text
顶部工具栏：
  新建 / 打开 / 保存 / 校验 / 发布 / 调试运行 / 停止

左侧：
  节点库

中间：
  无限画布

右侧：
  动态属性面板

底部：
  运行日志 / 节点输出 / 图像预览 / 错误列表
```

## Canvas 功能

MVP：

- 显示节点
- 拖动节点
- 从节点库添加节点
- 连接端口
- 删除节点
- 保存流程
- 发布运行态流程
- 调试运行
- 节点高亮

增强：

- 鼠标滚轮缩放
- 鼠标中键或空格拖动画布
- 贝塞尔连线
- 网格背景
- 吸附网格
- 框选
- 复制/粘贴
- 撤销/重做
- 自动布局
- 小地图

## 节点卡片

节点卡片显示：

- 图标
- 名称
- 类型
- 关键配置摘要
- 运行状态
- 耗时
- 端口

不显示全部配置。

## 属性面板

属性面板根据 `NodeSettingDescriptor` 动态生成。

编辑器类型：

- TextBox
- NumberBox
- ComboBox
- CheckBox
- CameraSelector
- LightSelector
- MotionSelector
- RecipeSelector
- VariableSelector
- ParameterGrid
- PathTemplateEditor
- DatabaseFieldMapper

## 变量选择器

数据引用示例：

```text
InputImage = {{ camera_callback_1.Image }}
```

变量选择器应按类型过滤。

## Runtime Debug

WPF Designer 通过 `FlowRuntimeEvent` 更新 UI。

Designer 不直接执行节点逻辑。
## 2026-06 Designer Notes

- `FlowDesignerControl` supports injected `NodeRegistry`, debug `IDeviceRegistry`, and `FlowDesignerOptions`. This allows Demo, tests, and future hosts to compose the Designer without editing the control internals.
- The property panel still renders from `NodeSettingDescriptor`, but binding-like settings such as `FrameBinding`, `ImageBinding`, `ResultBinding`, and group bindings now use the variable selector.
- The variable selector lists common `token.*` expressions and outputs from other nodes in the current design. It skips the selected node's own outputs to reduce accidental self-binding.
- New combo values are available for `CallbackMode`, `StreamOutputMode`, `FrameIndexSource`, `MatchMode`, `DuplicatePolicy`, `QueueFullMode`, and common queue names.
- Queue property editing includes `WaitForCompletion`; camera stream editing includes PerFrame fields; fusion/image outputs expose explicit image-role names such as `HeightMap`, `TextureImage`, and `ConfidenceMap`.
- Designer debug run continues to compile the current design and execute it through `FlowRunner`; production runtime must still use `.flowruntime` without Designer UI.
