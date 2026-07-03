# 04 - Node Development Guide

## 内置节点边界

SDK Core 内置基础流程节点和通用相机节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
camera.soft_trigger
camera.hard_trigger
camera.parameter.set
```

算法、图像保存、数据库保存、拼图、扫描、融合等节点仍应放在具体项目或项目专属节点库中。

## 新节点必须包含

```text
{NodeName}Node
{NodeName}NodeConfig
{NodeName}NodeFactory
{NodeName}Descriptor
```

节点必须实现 `IFlowNode`，需要随流程启动订阅外部事件的节点实现 `IFlowListenerNode`，并通过 `INodeFactory` 注册到 `NodeRegistry`。

常用命名空间：

```csharp
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Nodes;
```

## NodeType 命名

Core 内置节点使用稳定协议值。项目专属节点建议使用项目或领域前缀，例如：

```text
station.recipe.run
station.image.save
station.database.save
station.fusion.final
```

不要在 Core 中重新加入项目专属 NodeType 常量。

## Adapter 访问

相机节点只能通过 Core 中的 Adapter 契约访问相机：

```csharp
var camera = context.Devices.GetCamera(cameraId);
var frame = await camera.GrabOneAsync(cancellationToken);
```

硬触发节点在监听启动阶段订阅相机回调：

```csharp
camera.FrameArrived += OnFrameArrived;
```

回调线程只做轻量封装和后台投递，不直接执行后续节点。

禁止节点直接引用具体 SDK 类型或旧上位机业务对象。

## Descriptor 要求

Descriptor 必须声明：

- NodeType
- DisplayName
- Category
- Version
- InputPorts
- OutputPorts
- Settings
- Outputs

Designer 的节点库、属性面板和变量选择器都依赖 Descriptor。

## 测试要求

新增节点时应覆盖：

- 正常成功路径
- 缺少必要 Setting
- Adapter 不存在或返回失败
- Error / Timeout 路由
- 输出变量
- RuntimeEvent
- 序列化 / 发布兼容性

## Descriptor 枚举字段

`NodePortDescriptor.Direction` 使用 `FlowPortDirection`，`NodePortDescriptor.DataType`、`NodeSettingDescriptor.DataType` 和 `NodeOutputDescriptor.DataType` 使用 `FlowDataType`。

```csharp
new NodePortDescriptor
{
    Name = FlowPortNames.In,
    DisplayName = FlowPortNames.In,
    Direction = FlowPortDirection.Input,
    DataType = FlowDataType.Control,
    IsRequired = true
}
```

节点配置中的固定策略也应优先使用枚举，例如 `ConditionOperator.Equal`、`FlowDuplicatePolicy.Ignore`、`FlowLogLevel.Info`。当这些值进入 `NodeDefinition.Settings` 或流程文件时，由 `FlowEnumConverter` 转换为稳定字符串协议值。
