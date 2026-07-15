# 02 - Flow File Format

## Schema v2

当前开发版本只支持 `SchemaVersion = 2`。`.flowdesign` 和 `.flowruntime` 缺少版本号或版本号不是 2 时，反序列化器抛出 `UnsupportedFlowSchemaVersionException`；SDK 不提供 v1 迁移或兼容读取。

## 文件类型

`.flowdesign` 包含可发布的 `RuntimeFlowDefinition` 与画布坐标、缩放、折叠状态等设计器视图数据。

`.flowruntime` 只包含生产执行所需的数据：

- FlowId / FlowName / Version
- Nodes
- Edges（只表达控制流）
- Entries
- Settings

运行文件不得包含 WPF 类型、节点坐标、画布样式或 Designer ViewModel。

## 节点配置值

节点端口只用于连线和调度，不传递业务变量，也不存在 `InputBindings`。每个可编辑配置项统一保存为 `NodeSettingValue`：

```json
{
  "Mode": "Constant",
  "ConstantValue": 5000,
  "Selector": null
}
```

变量模式保留原常量，方便界面切回固定值：

```json
{
  "Mode": "Variable",
  "ConstantValue": 5000,
  "Selector": {
    "Scope": "NodeOutput",
    "Path": ["camera_1", "FrameId"]
  }
}
```

选择器范围：

- `NodeOutput`：Path 前两段为上游节点 ID 和输出名，后续段用于访问对象、字典或列表的子路径。
- `Token`：Path 从 Token 属性、Values 或 Metadata 开始解析。
- `TriggerInput`：Path 第一段为入口输入协议键，后续段用于访问对象、字典或列表的子路径。发布时至少要有一个声明该输入且能够到达目标节点的入口；同名输入在多个可达入口中的类型必须一致。

## 入口与触发协议

每个 `FlowEntryDefinition` 都完整声明触发方式、输入协议和入口级执行策略：

- `TriggerKind = Manual`：由设计器或宿主手动发起，从 `TargetNodeId` 开始执行。
- `TriggerKind = External`：由 PLC、MES、HTTP、相机 SDK 等外部宿主发起，从 `TargetNodeId` 开始执行。
- `TriggerKind = NodeEvent`：由 `SourceNodeId` 指向的 `IFlowListenerNode` 发起。监听事件先写入源节点输出，再按该源节点的 `OutputPort` 沿出边继续执行；`TargetNodeId` 不参与此类入口。

`Inputs` 中的每项由稳定键 `Name`、可选界面标签 `DisplayName`、`DataType`、`IsRequired`、`DefaultValue` 和 `Description` 组成。运行时拒绝未声明输入、缺少必填输入或无法转换到声明类型的输入。

`ExecutionPolicy` 的默认值为 `MaxConcurrentRuns = 1`、`QueueCapacity = 64`、`QueueFullBehavior = Reject`。队列容量只统计等待请求；满载时返回 `Rejected`，不会创建无界任务。

## Runtime 示例

```json
{
  "flowId": "core-basic",
  "flowName": "Core Basic Demo",
  "schemaVersion": 2,
  "version": "1.0.0",
  "nodes": [
    {
      "id": "set_result",
      "type": "variable.set",
      "name": "设置结果",
      "version": "1.0.0",
      "settings": {
        "VariableName": {
          "Mode": "Constant",
          "ConstantValue": "Inspection.Result",
          "Selector": null
        },
        "Value": {
          "Mode": "Variable",
          "ConstantValue": "OK",
          "Selector": {
            "Scope": "TriggerInput",
            "Path": ["inspectionResult"]
          }
        }
      }
    }
  ],
  "edges": [],
  "entries": [
    {
      "entryName": "ExternalInspection",
      "targetNodeId": "set_result",
      "sourceNodeId": null,
      "triggerKind": "External",
      "inputs": [
        {
          "name": "inspectionResult",
          "displayName": "检测结果",
          "dataType": "String",
          "isRequired": true,
          "defaultValue": null,
          "description": "本次检测的最终结果。"
        }
      ],
      "executionPolicy": {
        "maxConcurrentRuns": 1,
        "queueCapacity": 64,
        "queueFullBehavior": "Reject"
      }
    }
  ]
}
```

## 发布和校验

```text
.flowdesign
  -> FlowValidator
  -> validate setting selectors, upstream topology and data types
  -> FlowPublishService
  -> remove ViewState
  -> .flowruntime
```

变量输出按 `NodeId.OutputName` 写入运行时变量池。NodeOutput 选择器只能引用控制流拓扑中的前置节点输出；TriggerInput 选择器只能引用可达入口声明的输入；`Control` 类型不能绑定到配置项。类型兼容规则由 `FlowDataTypeCompatibility` 统一提供给 Validator 和 Designer。

固定策略值继续使用枚举公共 API，并在 JSON 的 `ConstantValue` 中序列化为稳定字符串，例如 `Equal`、`Ignore`、`Warning`。
