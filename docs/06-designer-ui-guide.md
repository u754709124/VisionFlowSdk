# 06 - Designer UI Guide

## 目标

WPF Designer 提供流程编辑、调试和发布体验，但不承载生产运行逻辑。

## 主要区域

```text
top toolbar: new / open / save / publish / debug run / stop
left: node palette
center: canvas
right: property panel
bottom: runtime debug panel
```

## 默认节点库

Designer 默认注册 Core 基础节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

宿主可以通过构造函数传入自己的 `NodeRegistry`，从而显示和调试项目专属设备/算法节点。

## 属性面板

属性面板根据 `NodeSettingDescriptor` 动态生成编辑器。绑定类设置可以使用变量选择器引用上游输出：

```text
{{ set_result.Value }}
{{ token.TokenId }}
```

## 调试运行

Designer 调试运行会把当前 `.flowdesign` 发布为运行态定义，再通过同一个 `FlowRunner` 执行，并订阅 `FlowRuntimeEvent` 高亮节点和显示日志。

生产进程必须使用 `.flowruntime`，不依赖 Designer 控件、画布或 ViewModel。
