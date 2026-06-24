# 03 - 实现最小执行引擎

请实现阶段 3：最小 FlowEngine 和 FlowRunner。

请先阅读：

- AGENTS.md
- docs/03-runtime-design.md

目标：

实现一个可以运行线性流程的最小执行引擎。

需要实现：

- FlowEngine
- FlowRunner
- FlowExecutionContext
- VariablePool
- IVariablePool
- IFlowEventSink
- InMemoryFlowEventSink
- RuntimeFlowPlan，如果你认为有必要

功能要求：

1. FlowEngine 根据 RuntimeFlowDefinition 创建 FlowRunner。
2. FlowRunner 支持 StartAsync、StopAsync、TriggerAsync。
3. TriggerAsync(entryName, token) 能找到 FlowEntryDefinition.TargetNodeId。
4. 节点执行完成后，根据 NodeExecutionResult.OutputPort 找到后续边继续执行。
5. 发布 NodeStarted、NodeCompleted、NodeFailed 事件。
6. 支持 Next 和 Error 端口。
7. 支持简单变量池，节点可以写入输出，后续节点可以读取。
8. 暂时不需要并行执行、复杂 JOIN、循环和断点。

测试要求：

1. 创建 TestNode A/B/C，验证 A -> B -> C 顺序执行。
2. 验证 NodeFailed 事件。
3. 验证 EntryName 不存在时返回明确异常。
4. 验证 RuntimeEvent 顺序。

验收条件：

1. build/test.ps1 通过。
2. Core 不引用 UI。
3. 执行引擎代码结构清晰，后续可扩展并行和汇合节点。
