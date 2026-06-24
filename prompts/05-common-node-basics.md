# 05 - 节点注册和公共节点基础

请实现阶段 5：公共节点注册机制和基础节点。

请先阅读：

- AGENTS.md
- docs/04-node-development-guide.md

在 Vision.Flow.Nodes 中实现：

- CommonNodeRegistration
- BaseNodeFactory<TConfig>，如果有必要
- LogNode
- DelayNode
- SplitNode
- VariableSetNode

每个节点都必须有：

- Config
- Node
- Factory
- Descriptor

节点类型：

- log.write
- delay.wait
- flow.split
- variable.set

要求：

1. CommonNodeRegistration.RegisterAll(NodeRegistry registry) 注册所有节点。
2. LogNode 发布日志事件。
3. DelayNode 支持 DelayMs。
4. SplitNode 输出多个端口，第一版至少支持 Next。
5. VariableSetNode 可以往 VariablePool 写入指定变量。
6. 每个节点 descriptor 包含端口、设置项、输出变量。

测试要求：

1. 注册后能通过 NodeRegistry 获取 factory。
2. LogNode 能执行并发布事件。
3. DelayNode 能执行。
4. VariableSetNode 写入变量后后续可读取。

验收条件：

1. build/test.ps1 通过。
2. 节点项目不引用 WPF、WinForms、SDK。
