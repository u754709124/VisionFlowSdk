# 04 - Node Development Guide

## 内置节点边界

SDK Core 只内置基础节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

设备、算法、图像保存、数据库保存、拼图、融合等节点应放在具体项目或项目专属节点库中。

## 新节点必须包含

```text
{NodeName}Node
{NodeName}NodeConfig
{NodeName}NodeFactory
{NodeName}Descriptor
```

节点必须实现 `IFlowNode`，通过 `INodeFactory` 注册到 `NodeRegistry`。

常用命名空间：

```csharp
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Nodes;
```

## NodeType 命名

Core 内置节点使用稳定协议值。项目专属节点建议使用项目或领域前缀，例如：

```text
station.camera.soft_trigger
station.recipe.run
station.image.save
station.database.save
station.fusion.final
```

不要在 Core 中重新加入项目专属 NodeType 常量。

## Adapter 访问

设备节点只能通过 Core 中的 Adapter 契约访问外部设备或上位机服务：

```csharp
var camera = context.Devices.GetCamera(cameraId);
await camera.SoftTriggerAsync(triggerContext, cancellationToken);
```

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

新增项目专属节点时，应覆盖：

- 正常成功路径
- 缺少必要 Setting
- Adapter 不存在或返回失败
- Error / Timeout 路由
- 输出变量
- RuntimeEvent
- 序列化/发布兼容性
