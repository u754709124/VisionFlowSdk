# 08 - Demo Guide

## Vision.Flow.Demo.WinForms

用途：

证明生产模式可以不打开 WPF Designer，直接加载 `.flowruntime` 执行。

功能：

- 加载 `.flowruntime`
- 注册 Fake Adapter
- 注册公共节点
- 启动 FlowRunner
- 手动触发流程
- 显示 RuntimeEvent 日志
- 显示输出摘要

禁止：

- 打开 WPF Designer
- 依赖 Canvas
- 依赖 NodeCard UI

## Vision.Flow.Demo.DesignerWpf

用途：

证明流程设计、调试、发布可用。

功能：

- 打开 `.flowdesign`
- 添加节点
- 连接节点
- 编辑属性
- 校验流程
- 使用 Fake Adapter 调试运行
- 发布 `.flowruntime`

## Sample Flows

```text
samples/flows/single-shot.flowdesign
samples/flows/single-shot.flowruntime
samples/flows/two-position-stitch.flowdesign
samples/flows/continuous-scan.flowdesign
```
## 2026-06 Demo Notes

- WinForms Demo can load a runtime file from the command line or Browse Runtime button, validate it, select an entry, create a representative fake `FlowToken`, and trigger the flow through `FlowRunner`.
- WinForms Demo registers fake adapters, common nodes, camera frame router, and queue registry without referencing `Vision.Flow.Designer.Wpf`.
- Designer WPF Demo hosts `FlowDesignerControl` with injected node registry, fake debug devices, and options.
- Designer property editing now exposes variable choices for input bindings and binding settings, including token fields and upstream node outputs.
