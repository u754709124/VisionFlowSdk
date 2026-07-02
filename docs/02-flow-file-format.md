# 02 - Flow File Format

## 文件类型

### `.flowdesign`

设计态文件，包含运行态流程和设计器视图状态：

- `RuntimeFlowDefinition`
- 节点坐标
- 画布缩放与偏�?
- 节点折叠等设计态状�?

### `.flowruntime`

生产运行态文件，只包含运行所需信息�?

- FlowId / FlowName / Version
- Nodes
- Edges
- Entries
- Settings
- InputBindings

`.flowruntime` 禁止包含 WPF 类型名、节点坐标、画布缩放、样式、Designer ViewModel �?Debug-only UI 状态�?

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

项目专属节点可以�?`settings` �?`inputBindings` 中定义自己的协议字段。Core 只校验通用结构、端口、绑定和 Core 内置节点规则；设�?算法节点的语义校验由具体项目实现�?
