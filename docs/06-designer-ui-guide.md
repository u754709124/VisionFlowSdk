# 06 - Designer UI Guide

## 目标

WPF Designer 提供流程编辑、调试和发布体验，但不承担生产运行逻辑。

## 主要区域

```text
top toolbar: new / open / save / publish / debug run / stop
left: node palette
center: canvas
right: property panel
bottom: runtime debug panel
```

## 默认节点库

Designer 默认注册 Core 内置节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
camera.soft_trigger
camera.hard_trigger
camera.parameter.set
```

宿主可以通过构造函数传入自己的 `NodeRegistry`，从而显示和调试项目专属算法、保存、数据库等节点。

嵌入设计器控件时引用：

```csharp
using Vision.Flow.Designer.Wpf.Controls;
```

## 属性面板

属性面板根据 `NodeSettingDescriptor` 动态生成编辑器。绑定类设置可以使用变量选择器引用上游输出：

```text
{{ set_result.Value }}
{{ token.TokenId }}
```

`CameraId` 默认提供 `Camera01` 作为示例下拉项；具体项目可以通过传入自己的节点注册表和调试设备来扩展实际体验。

## 调试运行

Designer 调试运行会把当前 `.flowdesign` 发布为运行态定义，再通过同一个 `FlowRunner` 执行，并订阅 `FlowRuntimeEvent` 高亮节点和显示日志。

生产进程必须使用 `.flowruntime`，不依赖 Designer 控件、画布或 ViewModel。

## 枚举编辑体验

Designer 根据 `FlowDataType` 选择属性编辑控件：`Boolean` 使用复选框，`Int32` / `Double` 使用数字文本转换，其它类型使用文本或下拉框。

端口连线规则使用 `FlowPortDirection` 判断输入/输出方向。条件操作符、AND Join 重复策略和日志等级的下拉项由 `FlowEnumConverter.GetWireValues<TEnum>()` 生成，并写回字符串协议值，保证保存后的 `.flowdesign` / `.flowruntime` 仍然可读。
