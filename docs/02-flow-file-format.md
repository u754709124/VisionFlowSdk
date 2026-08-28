# 02 - Flow File Format

## Schema v3

当前开发版本只支持 `SchemaVersion = 3`。`.flowdesign` 和 `.flowruntime` 缺少版本号或版本号不是 3 时，反序列化器抛出 `UnsupportedFlowSchemaVersionException`；SDK 不提供旧版本迁移或兼容读取。

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
    "Path": ["camera_1", "CaptureFrameId"]
  }
}
```

节点的端口、配置项和输出定义仍由注册工厂的 Descriptor 提供，不写入流程文件。动态节点可以把命令、模式等稳定选择保存为普通 `NodeSettingValue`，再由工厂根据反序列化后的 `NodeDefinition` 重新生成实例 Descriptor。`AffectsDescriptor` 等 Descriptor 元数据不进入 `.flowdesign` 或 `.flowruntime`，Schema 版本仍为 v3。

选择器范围：

- `NodeOutput`：Path 前两段为上游节点 ID 和输出名，后续段用于访问对象、字典或列表的子路径。
- `Token`：Path 从 Token 属性、Values 或 Metadata 开始解析。
- `TriggerInput`：Path 第一段为入口输入协议键，后续段用于访问对象、字典或列表的子路径。发布时至少要有一个声明该输入且能够到达目标节点的入口；同名输入在多个可达入口中的类型必须一致。

## 入口与触发协议

每个 `FlowEntryDefinition` 都完整声明触发方式、输入协议和入口级执行策略：

- `TriggerKind = Manual`：由设计器或宿主手动发起，从 `TargetNodeId` 开始执行。
- `TriggerKind = External`：由 PLC、MES、HTTP、相机 SDK 等外部宿主发起，从 `TargetNodeId` 开始执行。
- `TriggerKind = NodeEvent`：由 `SourceNodeId` 指向的 `IFlowListenerNode` 发起。监听事件先写入源节点输出，再按该源节点的 `OutputPort` 沿出边继续执行；`TargetNodeId` 不参与此类入口。

每条没有入边的链路起始节点都必须由入口覆盖，孤立节点同样按起始节点处理。普通起点需要 Manual 或 External 入口的 `TargetNodeId` 指向自身；监听起点必须由 NodeEvent 入口的 `SourceNodeId` 指向自身，Manual 入口不能替代监听订阅。校验失败返回稳定错误码 `ChainStartEntryMissing`。入口可以额外指向链路中间节点，但不能因此省略真正起点的入口。

`Inputs` 中的每项由稳定键 `Name`、可选界面标签 `DisplayName`、`DataType`、`IsRequired`、`DefaultValue` 和 `Description` 组成。运行时拒绝未声明输入、缺少必填输入或无法转换到声明类型的输入。

`ExecutionPolicy` 的默认值为 `MaxConcurrentRuns = 1`、`QueueCapacity = 64`、`QueueFullBehavior = Reject`。队列容量只统计等待请求；满载时返回 `Rejected`，不会创建无界任务。

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
            "Scope": "TriggerInput",
            "Path": ["inspectionResult"]
          }
        }
      }
    }
  ],
  "edges": [],
  "entries": [
    {
      "entryName": "ExternalInspection",
      "targetNodeId": "set_result",
      "sourceNodeId": null,
      "triggerKind": "External",
      "inputs": [
        {
          "name": "inspectionResult",
          "displayName": "检测结果",
          "dataType": "String",
          "isRequired": true,
          "defaultValue": null,
          "description": "本次检测的最终结果。"
        }
      ],
      "executionPolicy": {
        "maxConcurrentRuns": 1,
        "queueCapacity": 64,
        "queueFullBehavior": "Reject"
      }
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

`FlowPublishService.PublishToFile(document, path)` 是设计态文件落盘为生产运行文件的统一入口：它先按 Schema v3 创建独立运行态快照并完成全部校验，仅在校验成功后写入扩展名为 `.flowruntime` 的目标文件。校验失败时返回 `FlowPublishResult.Validation`，不会创建新文件，也不会覆盖已有运行文件。`FlowPublishResult.Runtime` 与输入设计文档不共享节点、配置值、变量选择器、执行策略或集合等可变对象。

变量输出按 `NodeId.OutputName` 写入运行时变量池。NodeOutput 选择器只能引用控制流拓扑中的前置节点输出；TriggerInput 选择器只能引用可达入口声明的输入；`Control` 类型不能绑定到配置项。类型兼容规则由 `FlowDataTypeCompatibility` 统一提供给 Validator 和 Designer。

Descriptor 可在 Object 类型的设置和输出上声明非序列化的 `ObjectType` CLR 元数据。
流程文件仍只保存结构化 Selector：对象根路径为 `[NodeId, OutputName]`，首层成员路径为
`[NodeId, OutputName, MemberName]`。协议只允许展开一层公开可读属性或公开字段，更深路径
会在发布校验中被拒绝；`ObjectType` 本身不写入 `.flow` 或 `.flowruntime`。

`FlowValidator` 按每个 `NodeDefinition` 解析当前实例 Descriptor，再校验必填配置、端口、输出绑定和 `DefaultOutputs`。动态 Descriptor 解析失败时返回 `NodeDescriptorResolutionFailed`，不会把提供程序异常直接抛出到发布调用方。

固定策略值继续使用枚举公共 API，并在 JSON 的 `ConstantValue` 中序列化为稳定字符串，例如 `Equal`、`Ignore`、`Warning`。

### IF 条件节点 v3

`condition.if` v3 使用 `LeftValue`、`Operator`、`RightValue` 三个稳定设置键。`LeftValue` 必须是结构化变量选择器；`RightValue` 可以是固定值或变量。旧版 `LeftBinding`、`RightBinding` 设置键以及 `Contains`、`IsNull`、`IsNotNull` 操作符不再兼容，旧流程必须在设计器中重新配置后发布。

- `Int32`、`Int64`、`Double` 属于同一个数值兼容族，支持 `<`、`<=`、`==`、`!=`、`>=`、`>`。
- 普通字符串仅支持区分大小写的 `==`、`!=`。
- 枚举在线路协议中仍以字符串成员名保存，但两侧必须携带同一个 `EnumType`；右侧固定值使用该枚举的成员下拉框。
- 左值重新绑定到其他类型后，已有操作符和右值会保留并显示校验错误，不会静默改写用户配置。

## 环境变量

Schema v3 的 `Runtime` 可包含 `EnvironmentVariables`。每项使用不可变 `Id`
作为流程协议标识，`Name` 仅用于用户界面，首期 `DataType` 只允许 `Int32`、
`Boolean`、`String`，并要求提供类型匹配的 `DefaultValue`。节点配置通过
`VariableSelectorScope.EnvironmentVariable` 和单段 `Path: [variableId]` 引用；
改显示名称不会破坏绑定。发布会校验并深拷贝定义，无环境变量的 v3 文件继续按
空集合加载。

## Session 全局变量

Schema v3 的 `Runtime.GlobalVariables` 是独立于只读环境变量的可变 Session 状态。
每项包含稳定且唯一的 `Id`、唯一显示名 `Name`、`DataType` 和非空
`DefaultValue`；环境变量和 Session 全局变量均保持声明的 CLR 类型，不做字符串或数字间的隐式转换；
类型只允许 `String`、`Int32`、`Boolean`、`DateTime`，其中 String 允许空字符串，
DateTime 使用 JSON ISO 8601 表示并在运行时恢复为强类型 `System.DateTime`。业务代码使用本地时间，
变量存储不检查 `DateTime.Kind`，也不执行 UTC 与本地时间之间的转换。
流程文件只保存定义和默认值，不保存运行值。

节点通过 `VariableSelectorScope.GlobalVariable` 和单段 `Path: [variableId]` 引用。
改名不影响引用；删除定义、重复 Id/Name、改变类型导致现有绑定不兼容时，发布校验
会拒绝流程。`variable.set` 缺少 `TargetScope` 时仍按旧协议写入 FlowRun 局部变量；
`TargetScope=GlobalVariable` 时以 `GlobalVariableId` 选择目标，并按目标定义生成精确的
`Value` 类型约束。

结构化字段映射常量使用有序对象集合，每项保存明确的 `AttributeName` 和 `Source`
选择器。`AttributeName` 是稳定协议键，不从变量或节点名称推导。
