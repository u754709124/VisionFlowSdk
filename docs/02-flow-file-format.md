# 02 - Flow File Format

## 文件类型

### `.flowdesign`

设计态文件，供 WPF Designer 打开。

包含：

- RuntimeFlowDefinition
- 节点坐标
- 画布缩放
- 画布偏移
- 节点折叠状态
- 调试 UI 状态

### `.flowruntime`

运行态文件，供生产 WinForms 上位机加载执行。

包含：

- FlowId
- FlowVersion
- Nodes
- Edges
- Entries
- Settings
- VariableBindings

禁止包含：

- WPF 类型名
- 节点坐标
- Canvas 缩放
- 节点颜色
- Designer ViewModel 状态

## `.flowdesign` 示例

```json
{
  "flowId": "Station01_Main",
  "flowName": "Station01 Main Flow",
  "schemaVersion": 1,
  "runtime": {
    "flowId": "Station01_Main",
    "flowName": "Station01 Main Flow",
    "version": "1.0.0",
    "nodes": [],
    "edges": [],
    "entries": []
  },
  "view": {
    "zoom": 1.0,
    "offsetX": 0,
    "offsetY": 0,
    "nodes": {}
  }
}
```

## `.flowruntime` 示例

```json
{
  "flowId": "Station01_Main",
  "flowName": "Station01 Main Flow",
  "schemaVersion": 1,
  "version": "1.0.0",
  "nodes": [
    {
      "id": "camera_trigger_1",
      "type": "camera.soft_trigger",
      "name": "Camera Soft Trigger",
      "version": "1.0.0",
      "settings": {
        "CameraId": "Camera01",
        "TimeoutMs": 1000
      },
      "inputBindings": {}
    }
  ],
  "edges": [
    {
      "fromNodeId": "camera_trigger_1",
      "fromPort": "Next",
      "toNodeId": "camera_callback_1",
      "toPort": "In"
    }
  ],
  "entries": [
    {
      "entryName": "ManualStart",
      "targetNodeId": "camera_trigger_1"
    }
  ]
}
```

## 发布流程

```text
.flowdesign
    -> FlowValidator
    -> FlowPublishService
    -> 移除 ViewState
    -> .flowruntime
```

## 版本字段

每个流程文件应该包含：

- SchemaVersion
- FlowVersion
- NodeType
- NodeVersion
## 2026-06 Runtime Fields

The published `.flowruntime` format remains UI-free. New runtime-only settings are allowed in node `settings` or `inputBindings`:

- `camera.image_callback`: `CallbackMode`, `MatchMode`, `StreamOutputMode`, `ExpectedFrameCount`, `FrameTimeoutMs`, `AutoStopAfterExpectedFrameCount`, `FrameIndexSource`, `StartFrameIndex`, and matching bindings such as `TriggerIdBinding` or `ScanGroupIdBinding`.
- queue-enabled nodes: `UseQueue`, `QueueName`, `QueueCapacity`, `QueueMaxDegreeOfParallelism`, `QueueFullMode`, and `WaitForCompletion`.
- group/scan nodes: `DuplicatePolicy`, `RequireContinuousShotIndex`, `RequireContinuousFrameIndex`, `FirstShotIndex`, and `FirstFrameIndex`.
- binding settings such as `FrameBinding`, `ImageBinding`, `CaptureGroupIdBinding`, `ScanGroupIdBinding`, `FrameGroupBinding`, and `ScanGroupResultBinding`.

`FlowValidator` rejects invalid StreamFrames settings, invalid queue settings, invalid duplicate policies, and missing binding sources. `.flowruntime` must still exclude WPF view state, canvas coordinates, zoom, styles, and designer-only debug state.

## 2026-06 Sample Flow Updates

- `single-shot.flowruntime` now includes `light.control` before `camera.soft_trigger`.
- `continuous-scan.flowdesign` starts from `camera.image_callback` with `CallbackMode=StreamFrames` and `StreamOutputMode=PerFrame`; each `Frame` continuation drives `frame.preprocess`.
- `.flowruntime` remains runtime-only. New queue, stream, and image-kind behavior is expressed through node settings and outputs, not through Designer view state.
