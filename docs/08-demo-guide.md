# 08 - Demo Guide

## Vision.Flow.Demo.WinForms

用途：证明生产模式可以不打开 WPF Designer，直接加载 `.flowruntime` 并通过 `FlowRunner` 执行。

当前 Demo 使用 `samples/flows/core-basic.flowruntime`，注册 Core 内置节点。

功能：

- 加载 `.flowruntime`
- 注册 Core 内置节点
- 启动 / 停止 `FlowRunner`
- 手动触发入口
- 显示 `FlowRuntimeEvent`
- 显示输出摘要

禁止：

- 打开 WPF Designer
- 依赖 Canvas
- 依赖 NodeCard UI

## Vision.Flow.Demo.DesignerWpf

用途：证明流程设计、调试和发布能力可用。

默认加载 Core 基础样例。具体项目可以宿主 `FlowDesignerControl` 并注入自己的节点注册表和调试设备。

## Sample Flows

```text
samples/flows/core-basic.flowdesign
samples/flows/core-basic.flowruntime
```
