# 04 - Node Development Guide

## 每个节点必须包含

```text
{NodeName}Node.cs
{NodeName}NodeConfig.cs
{NodeName}NodeFactory.cs
{NodeName}NodeDescriptor.cs
```

示例：

```text
CameraSoftTriggerNode.cs
CameraSoftTriggerNodeConfig.cs
CameraSoftTriggerNodeFactory.cs
CameraSoftTriggerNodeDescriptor.cs
```

## NodeType 命名

使用小写点分命名：

```text
camera.soft_trigger
camera.set_parameters
camera.image_callback
light.control
motion.notify
recipe.run
image.save
database.save
join.and
group.frame_join
scan.group_join
fusion.final_3d_2d
```

## Node 实现模板

```csharp
public sealed class CameraSoftTriggerNode : IFlowNode
{
    private readonly CameraSoftTriggerNodeConfig _config;

    public string NodeId { get; }

    public CameraSoftTriggerNode(
        string nodeId,
        CameraSoftTriggerNodeConfig config)
    {
        NodeId = nodeId;
        _config = config;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(
        FlowExecutionContext context,
        FlowToken token,
        CancellationToken cancellationToken)
    {
        var camera = context.Devices.GetCamera(_config.CameraId);

        var triggerId = Guid.NewGuid().ToString("N");

        token.Set("TriggerId", triggerId);

        await camera.SoftTriggerAsync(
            new CameraTriggerContext
            {
                CameraId = _config.CameraId,
                TriggerId = triggerId,
                TokenId = token.TokenId,
                TriggerTime = DateTime.Now
            },
            cancellationToken);

        return NodeExecutionResult.Success(token, "Next");
    }
}
```

## Descriptor 要求

每个 Descriptor 必须定义：

- NodeType
- DisplayName
- Category
- IconKey
- InputPorts
- OutputPorts
- Settings
- OutputVariables

## 常用端口

```text
In
Next
Error
Timeout
Cancel
```

## 测试要求

每个节点应测试：

- 正常成功路径。
- 缺少必要 Setting。
- 设备不存在。
- 错误路径。
- 输出变量。
- RuntimeEvent。
## 2026-06 Common Nodes

New or enhanced node types:

- `condition.if`: evaluates a boolean expression or binding and routes `True` / `False`.
- `join.and`: waits for all configured inputs sharing the same join key.
- `motion.notify`, `motion.move_to`, `motion.wait_in_position`: access motion devices only through `IMotionAdapter`.
- `camera.image_callback`: supports next-frame matching and basic stream collection through `ICameraFrameRouter`.
- `recipe.run`, `image.save`, `database.save`: can optionally execute adapter work through named bounded queues.
- `group.frame_join`, `image.stitch`, `scan.group_join`, `fusion.final_3d_2d`: support binding-driven inputs, duplicate policies, and continuous index validation.

Node descriptors must expose every user-editable setting, including queue settings and binding settings, so Designer and Validator can stay descriptor-driven.
