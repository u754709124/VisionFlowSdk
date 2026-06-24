# 06 - 相机三节点

请实现阶段 6：相机三节点。

请先阅读：

- AGENTS.md
- docs/04-node-development-guide.md
- docs/05-device-adapter-guide.md

在 Vision.Flow.Nodes 中实现：

1. CameraSetParameterNode
   NodeType: camera.set_parameters

2. CameraSoftTriggerNode
   NodeType: camera.soft_trigger

3. CameraImageCallbackNode
   NodeType: camera.image_callback

要求：

## CameraSetParameterNode

Settings:

- CameraId
- Parameters: parameter name + value binding/constant value
- TimeoutMs

要求：

- 通过 ICameraAdapter.SetParameterAsync 设置参数。
- 支持常量值。
- 预留变量绑定能力。

## CameraSoftTriggerNode

Settings:

- CameraId
- TimeoutMs

要求：

- 创建 TriggerId。
- 写入 Token 或 VariablePool：
  - CameraId
  - TriggerId
  - TriggerTime
- 调用 ICameraAdapter.SoftTriggerAsync。
- 不直接处理图像。

## CameraImageCallbackNode

Settings:

- CameraId
- TimeoutMs
- MatchMode，第一版可以支持 TriggerId

要求：

- 等待 FakeCameraAdapter.FrameArrived。
- 匹配 CameraId 和 TriggerId。
- 输出：
  - Image
  - Frame
  - FrameId
  - GrabTime
  - Metadata
- 超时返回 Timeout 结果或走 Timeout 输出端口。

可能需要实现 CameraFrameRouter。
如果实现 CameraFrameRouter，请放在 Core 或 DeviceAdapters 中，保持 UI 无关。

测试要求：

1. 使用 FakeCameraAdapter 跑通：
   camera.set_parameters -> camera.soft_trigger -> camera.image_callback
2. 验证 CameraImageCallbackNode 输出 Image 和 FrameId。
3. 验证超时场景。
4. 验证 TriggerId 匹配。

验收条件：

1. build/test.ps1 通过。
2. Nodes 不引用具体相机 SDK。
3. 相机节点 descriptor 可供 WPF 属性面板使用。
