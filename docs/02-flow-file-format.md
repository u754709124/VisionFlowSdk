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
- `TriggerInput`：协议预留，当前发布校验会拒绝使用。

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
            "Scope": "NodeOutput",
            "Path": ["inspect_1", "Result"]
          }
        }
      }
    }
  ],
  "edges": [],
  "entries": [
    {
      "entryName": "ManualStart",
      "targetNodeId": "set_result"
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

变量输出按 `NodeId.OutputName` 写入运行时变量池。NodeOutput 选择器只能引用控制流拓扑中的前置节点输出；`Control` 类型不能绑定到配置项。类型兼容规则由 `FlowDataTypeCompatibility` 统一提供给 Validator 和 Designer。

固定策略值继续使用枚举公共 API，并在 JSON 的 `ConstantValue` 中序列化为稳定字符串，例如 `Equal`、`Ignore`、`Warning`。
