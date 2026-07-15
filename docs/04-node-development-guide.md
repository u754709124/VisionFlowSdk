# 04 - Node Development Guide

## 内置节点边界

SDK Core 只内置基础流程节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

相机、算法、图像保存、数据库保存、拼图、扫描、融合等节点仍应放在具体项目或项目专属节点库中。

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

项目专属相机节点只能通过 Core 中的 Adapter 契约访问相机：

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

每个 `NodeSettingDescriptor` 还必须明确声明：

- `BindingMode`：`ConstantOnly` 或 `ConstantOrVariable`
- `EvaluationPhase`：`Execution` 或 `ListenerStart`
- `AllowedVariableSources`：允许的 NodeOutput、Token、TriggerInput 范围

执行期配置在节点中统一读取：

```csharp
var timeoutMs = context.GetSettingValue<int>("TimeoutMs");
```

不要为控制输入端口创建变量绑定。节点输出通过 `VariableSelector.ForNodeOutput(nodeId, outputName)` 绑定到具体配置项。

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

节点 `Descriptor.DisplayName`、`Descriptor.Description` 和面向用户展示的 `Descriptor.Category` 必须使用中文；`NodeType`、端口名、设置键和输出名属于稳定流程协议，继续使用兼容的英文标识，不得为了界面中文化修改协议值。

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

`NodeDefinition.Settings` 的值必须使用 `NodeSettingValue.ForConstant(value)` 或 `NodeSettingValue.ForVariable(selector, retainedConstant)` 创建。Factory 只能读取常量作为构造默认值；动态值必须在 `ExecuteAsync` 中读取。
