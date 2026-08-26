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
- `AllowedVariableSources`：允许的 NodeOutput、Token、TriggerInput、EnvironmentVariable 范围
- `AffectsDescriptor`：该常量变化后是否需要重新解析实例 Descriptor

配置项通过 `DataType` 对常量和变量实施统一的强类型约束。变量来源的
`FlowDataType` 必须与配置项完全一致，不执行数值扩宽、`Object` 向具体类型转换
或字符串隐式转换。需要范围、格式或枚举成员等业务约束时，可以声明同步单项
`Validator`：

当 `DataType = FlowDataType.Object` 且协议实际承载类型已知时，设置和输出必须通过
`ObjectType` 声明 CLR 类型。类型化 Object 按 `IsAssignableFrom` 校验：具体来源可绑定
到相同类型或其接口/基类目标，未声明 `ObjectType` 的来源不能绑定到类型化目标；未声明
`ObjectType` 的目标仍作为确有必要的通用 Object 接受任意 Object 来源。Designer 会让
输出对象本身保持可选，并从其公开可读实例属性和公开实例字段生成一层成员候选，不递归
展开成员的成员；发布校验也只接受这一层路径。

字符串协议承载枚举值时，设置与输出应同时通过 `EnumType` 声明具体枚举类型。
Designer 会为常量自动生成枚举成员下拉框，并且变量选择和发布校验只接受
`DataType` 与 `EnumType` 都一致的来源；流程文件仍保存原有字符串 wire value：

```csharp
new NodeSettingDescriptor
{
    Name = "Mode",
    DisplayName = "运行模式",
    DataType = FlowDataType.String,
    EnumType = typeof(RunMode),
    DefaultValue = FlowEnumConverter.ToWireValue(RunMode.Auto),
    BindingMode = NodeSettingBindingMode.ConstantOrVariable
}
```

```csharp
new NodeSettingDescriptor
{
    Name = "TimeoutMs",
    DisplayName = "超时时间",
    DataType = FlowDataType.Int32,
    BindingMode = NodeSettingBindingMode.ConstantOrVariable,
    Validator = delegate(object value)
    {
        var timeout = (int)value;
        return timeout >= 0 && timeout <= 60000
            ? null
            : "超时时间必须在 0～60000 毫秒之间。";
    }
}
```

校验器接收已经转换为目标 `DataType` 的非空常量；返回空字符串表示通过，返回
非空字符串会在设计器和发布校验中阻止应用或发布。可选空值不会调用校验器，
校验器异常也会转换为结构化校验错误。首版校验器不读取其他配置项，也不校验
运行时变量的实际值。Descriptor 和 Validator 都不进入流程文件。

执行期配置在节点中统一读取：

```csharp
var timeoutMs = context.GetSettingValue<int>("TimeoutMs");
```

不要为控制输入端口创建变量绑定。节点输出通过 `VariableSelector.ForNodeOutput(nodeId, outputName)` 绑定到具体配置项。
流程环境变量通过 `VariableSelector.ForEnvironmentVariable(variableId)` 绑定；
只有 `Execution + ConstantOrVariable` 配置项能够使用。
Session 全局变量通过 `VariableSelector.ForGlobalVariable(variableId)` 绑定，并在节点
执行时从 `context.GlobalVariables` 读取当前值。需要一次读取多个全局值时应调用
`CreateSnapshot(ids)`，不要逐项读取后自行假设一致性。

## 动态 Descriptor

端口、命令参数或输出会随节点设置变化时，Factory 保持实现 `INodeFactory`，并额外实现可选接口：

```csharp
public sealed class CommandNodeFactory :
    INodeFactory,
    IInstanceNodeDescriptorProvider
{
    public NodeDescriptor Descriptor
    {
        get { return CreatePaletteDescriptor(); }
    }

    public NodeDescriptor GetDescriptor(NodeDefinition definition)
    {
        return CreateCommandDescriptor(definition);
    }
}
```

静态 `Descriptor` 是节点库和新建节点的基础契约，必须包含用于选择实例形状的设置及默认值；`NodeRegistry.ResolveDescriptor(definition)` 才返回节点实例当前生效的契约。影响实例契约的设置必须设为 `AffectsDescriptor = true`、`BindingMode = ConstantOnly`，不得依赖运行时变量决定端口或输出。

实例 Descriptor 不持久化。Factory 必须只根据 `NodeDefinition` 和稳定项目元数据确定性生成结果，并保持 `NodeType` 与注册值一致。解析失败会由 `FlowValidator` 报告为 `NodeDescriptorResolutionFailed`；普通 Factory 无需修改，会自动回退静态 `Descriptor`。

当 Descriptor 还依赖当前流程定义（例如 `variable.set` 的目标全局变量类型）时，
Factory 实现 `IFlowDefinitionNodeDescriptorProvider`。解析优先级固定为“流程感知 →
实例感知 → 静态 Descriptor”，调用 `NodeRegistry.ResolveDescriptor(flow, definition)`；
Designer、Validator 和发布服务使用同一结果。

需要编辑有序的“显式目标字段 + VariableSelector”集合时，将 Object 设置声明为
`NodeSettingEditorKind.VariableSelectorMappings`，并用 `AllowedVariableSources` 限制来源。
属性面板会提供新增、删除、上移、下移、字段名和变量选择器列，并原样保存
`VariableSelectorFieldMapping` 协议。

## NodeExecutionPolicy 协议

Schema v2 的每个节点在 `.flowdesign` 和 `.flowruntime` 中都必须按下面的稳定结构序列化。生产序列化器始终输出完整的 `ExecutionPolicy`；读取缺失或 `null` 的策略时使用默认值，不提供 v1 兼容或迁移逻辑。

```json
{
  "Id": "inspect_1",
  "Type": "station.inspect.run",
  "Name": "执行检测",
  "Version": "1.0.0",
  "Settings": {},
  "ExecutionPolicy": {
    "TimeoutMs": 0,
    "MaxConcurrentExecutions": 1,
    "RetryPolicy": {
      "Enabled": false,
      "MaxRetries": 3,
      "RetryIntervalMs": 1000
    },
    "FailureStrategy": "StopFlow",
    "DefaultOutputs": {}
  }
}
```

协议规则：

- `TimeoutMs = 0` 继承全局节点超时；负数非法。
- `MaxConcurrentExecutions` 必须大于 0。当前版本只固化协议和校验，尚不作为节点级调度门。
- `MaxRetries` 是首次失败后的附加重试次数，不包含首次执行，必须大于或等于 0。
- `RetryIntervalMs` 是固定重试间隔，必须大于或等于 0。
- `RetryPolicy.Enabled = false` 时只尝试一次，即使 `MaxRetries` 为正数也不重试。
- `FailureStrategy` 使用稳定字符串 `StopFlow`、`ErrorBranch` 或 `DefaultOutputs`。
- `DefaultOutputs` 顶层键按大小写不敏感比较；序列化、反序列化和发布克隆都必须保持该语义。

## 发布校验

`FlowValidator` 对节点执行策略执行以下发布前校验：

- 数值范围或枚举值无效时返回 `NodeExecutionPolicyInvalid`。
- `FailureStrategy = ErrorBranch` 时，Descriptor 必须声明名称为 `Error`、方向为 Output、类型为 `Control` 的端口；否则返回 `NodeErrorPortMissing`。
- `FailureStrategy = DefaultOutputs` 时，字典必须覆盖 `NodeDescriptor.Outputs` 声明的全部输出，不能包含未声明键，并且每个非空值必须能转换到对应 `FlowDataType`；否则返回 `NodeDefaultOutputInvalid`。

以上端口、必填设置、变量输出和默认输出校验均使用节点实例解析后的 Descriptor。切换动态命令后，已经不存在的下游输出绑定不会被静默改写，发布校验会返回相应的 `VariableOutputMissing`。

发布服务会深拷贝策略及默认输出。输入策略、重试策略或默认输出字典为 `null` 时均回落到模型默认值，运行态文件不依赖 Designer 对象。

## 失败分类约定

节点配置统一通过 `FlowExecutionContext.GetSettingValue` 读取，以便 Runtime 明确区分失败类型：

- Selector 无效、变量不可解析或变量值转换失败使用 `SettingBindingException`，归类为 `NodeFailureKind.Binding`。
- Setting 结构、常量配置或常量值转换失败使用 `NodeConfigurationException`，归类为 `NodeFailureKind.Configuration`。
- 节点返回失败或抛出其他异常归类为 `NodeFailureKind.Execution`。
- 超时由统一包装器归类为 `NodeFailureKind.Timeout`；取消令牌导致的中止归类为 `NodeFailureKind.Cancelled`。

节点不应自行实现策略重试、超时计时或失败分支调度，也不应吞掉 `OperationCanceledException`。长耗时节点必须把传入的 `CancellationToken` 继续传给 Adapter、I/O 和等待操作，使超时、停止与重试等待取消能够及时收敛。

## 测试要求

新增节点时应覆盖：

- 正常成功路径
- 缺少必要 Setting
- Adapter 不存在或返回失败
- Error / Timeout 路由
- 重试成功、重试耗尽和重试等待取消
- Binding / Configuration 不重试
- StopFlow / ErrorBranch / DefaultOutputs 三种失败策略
- 输出变量
- RuntimeEvent 的 Attempt / FailureKind / FailureStrategy 数据
- Schema v2 执行策略 round-trip、缺失策略默认值与发布校验

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
