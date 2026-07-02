# 02 - Flow File Format

## 文件类型

### `.flowdesign`

设计态文件，包含运行态流程和设计器视图状态：

- `RuntimeFlowDefinition`
- 节点坐标
- 画布缩放与偏移
- 节点折叠等设计态状态

### `.flowruntime`

生产运行态文件，只包含运行所需信息：

- FlowId / FlowName / Version
- Nodes
- Edges
- Entries
- Settings
- InputBindings

`.flowruntime` 禁止包含 WPF 类型名、节点坐标、画布缩放、样式、Designer ViewModel 或 Debug-only UI 状态。

## Runtime 示例

```json
{
  "flowId": "core-basic",
  "flowName": "Core Basic Demo",
  "schemaVersion": 1,
  "version": "1.0.0",
  "nodes": [
    {
      "id": "set_result",
      "type": "variable.set",
      "name": "Set Result",
      "version": "1.0.0",
      "settings": {
        "VariableName": "Inspection.Result",
        "Value": "OK"
      },
      "inputBindings": {}
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

## 发布流程

```text
.flowdesign
  -> FlowValidator
  -> FlowPublishService
  -> remove ViewState
  -> .flowruntime
```

## 扩展节点

项目专属节点可以在 `settings` 和 `inputBindings` 中定义自己的协议字段。Core 只校验通用结构、端口、绑定和 Core 内置节点规则；设备/算法节点的语义校验由具体项目实现。

## 枚举与文件协议值

SDK 公共 API 中的固定集合使用枚举，例如 `FlowPortDirection`、`FlowDataType`、`ConditionOperator`、`FlowDuplicatePolicy`、`FlowLogLevel` 和相机帧模式枚举。

流程文件仍写入可读字符串协议值，不写入枚举数字。例如：

```json
{
  "settings": {
    "Operator": "Equal",
    "DuplicatePolicy": "Ignore",
    "Level": "Warning"
  }
}
```

序列化由 `FlowEnumConverter` 统一把枚举转换为字符串，避免枚举顺序调整影响 `.flowruntime` 兼容性。项目专属节点如果需要开放的数据类型，应使用 `FlowDataType.Object`，并在自己的设置或 Adapter 契约中定义更细的业务语义。
