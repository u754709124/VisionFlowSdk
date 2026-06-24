# 02 - 实现 Core 基础模型

请实现阶段 2：Vision.Flow.Core 基础模型。

请先阅读：

- AGENTS.md
- docs/02-flow-file-format.md
- docs/03-runtime-design.md

在 Vision.Flow.Core 中实现以下目录和类型：

Definitions:

- FlowDesignDocument
- FlowViewState
- NodeViewState
- RuntimeFlowDefinition
- NodeDefinition
- EdgeDefinition
- FlowEntryDefinition
- NodeDescriptor
- NodePortDescriptor
- NodeSettingDescriptor
- NodeOutputDescriptor
- VariableBinding

Runtime:

- FlowToken
- IFlowNode
- NodeExecutionResult
- FlowExecutionContext
- IFlowRunner
- FlowRuntimeEvent
- FlowRuntimeEventType
- NodeRuntimeState

Registry:

- INodeFactory
- NodeRegistry

Serialization:

- FlowDesignSerializer
- RuntimeFlowSerializer

要求：

1. 所有模型可 JSON 序列化和反序列化。
2. 不引用 WPF、WinForms、具体设备 SDK。
3. FlowToken 支持 Set/Get/TryGet。
4. NodeExecutionResult 支持 Success、Failure、Timeout 静态方法。
5. NodeDescriptor 能描述端口、属性和输出变量。
6. 增加序列化 round-trip 测试。
7. 增加 FlowToken 测试。
8. 保持代码简单，不要实现复杂引擎。

验收条件：

1. build/test.ps1 通过。
2. 测试覆盖 FlowToken 和 JSON round-trip。
3. 说明新增类型和设计取舍。
