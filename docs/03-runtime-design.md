# 03 - Runtime Design

## 目标

Runtime 必须能在没有 WPF Designer 的生产进程中加载 `.flowruntime` 并执行。

## 核心类型

```text
FlowEngine
FlowRunner
FlowExecutionContext
FlowToken
VariablePool
NodeRegistry
FlowRuntimeEvent
RuntimeFlowPlan
```

## 执行模型

```text
TriggerAsync(entryName, token)
  -> find Entry.TargetNodeId
  -> execute node
  -> write outputs to VariablePool
  -> publish FlowRuntimeEvent
  -> route by OutputPort
```

`RuntimeFlowPlan` 在运行前索引入口、节点和按输出端口分组的连线，保证一个输出端口可以按定义顺序扇出到多个下游节点。

## Core 基础节点

Core 内置节点只覆盖流程控制和变量能力：

- `delay.wait`
- `log.write`
- `variable.set`
- `flow.split`
- `join.and`
- `condition.if`

设备、算法、存储、拼图和融合节点由具体项目实现。

## Runtime 服务

`FlowExecutionContext` 仍携带可选服务：

- `IDeviceRegistry`
- `ICameraFrameRouter`
- `IFlowTaskQueueRegistry`
- `IFlowContinuationDispatcher`

这些服务是扩展节点的基础契约，不表示 SDK 内置对应设备节点。

## 线程规则

- 节点实现使用 `async` / `await` 和 `CancellationToken`。
- 相机回调等外部事件线程只做轻量封装和转发。
- 重算法、保存、数据库等长耗时工作由具体项目节点自行放入后台任务或有界队列。
- 生产运行不依赖 Designer UI。
