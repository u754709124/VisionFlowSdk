# 06 - Designer UI Guide

## 目标

WPF Designer 提供流程编辑、调试和发布体验，但不承担生产运行逻辑。

嵌入式宿主通过 `FlowDesignerOptions.SettingConstantOptionsProvider` 提供
`NodeSettingConstantOption` 候选项。`DisplayName` 只用于属性面板展示，`Value`
会按 Descriptor 的 `FlowDataType` 转换后写入流程定义；宿主不再提供裸字符串候选。

## 主要区域

```text
顶部命令栏：编辑模式 / 调试运行 / New / Sample / Open / Save / Publish / 运行 / 停止
左侧 244 px：节点库
中间：弹性画布
右侧 380 px：节点属性
底部：36 px 收起或 190 px 展开的运行调试抽屉
```

`FlowDesignerOptions.ShowStandaloneDocumentCommands` 默认为 `true`，保持独立设计器原有的 New / Sample / Open / Save / Publish 命令。业务宿主统一管理自己的复合配置文件时可设为 `false`；此时仍保留“编辑模式 / 调试运行 / 运行 / 停止”。

## 默认现代主题

`FlowDesignerTheme.CreateModern()` 提供可复用的 SDK 默认资源字典，包含字体、语义色、40 px 文本框与下拉框、主次按钮、固定值/变量分段按钮、专用变量选择器、绿色开关、卡片边框、行内错误、折叠器、菜单、Tooltip 和紧凑滚动条。文本框、下拉框、下拉候选行和变量选择器均使用 SDK 自绘圆角模板及 WPF 矢量箭头，不回退到系统原生灰色控件；设计器其它图标也全部由 WPF `Path` 绘制，不依赖字体图标或第三方 UI 库。

设计器自身、外置命令栏、属性面板和 SDK 弹窗都会合并一份独立主题字典，保证脱离原控件视觉树后仍能解析资源。宿主需要让自有外壳保持同一风格时，可以把 `FlowDesignerTheme.CreateModern()` 合并到宿主资源；这些资源是 SDK 的固定默认语义，不承诺通过祖先同名资源覆盖设计器内部主题。

SDK WPF Demo 使用 52 px 深色自绘标题栏，提供真实的拖动、双击最大化、最小化、最大化/还原、关闭和窗口边缘缩放行为。关闭前同样会处理未应用属性草稿。

## 默认节点库

Designer 默认注册 Core 内置节点：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

节点库和节点卡片显示 Descriptor 提供的中文名称与中文描述；`NodeType` 仅作为稳定流程协议标识，不作为默认副标题展示。用户自行修改的节点实例名称仍按流程文件原值显示。

宿主可以通过构造函数传入自己的 `NodeRegistry`，从而显示和调试项目专属相机、算法、保存、数据库等节点。

嵌入设计器控件时引用：

```csharp
using Vision.Flow.Designer.Wpf.Controls;
```

## 嵌入式宿主 API

业务应用可以隐藏设计器自带的文件命令，并由宿主加载、重置和捕获设计态文档：

```csharp
var designer = new FlowDesignerControl(nodes, null, new FlowDesignerOptions
{
    LoadSampleOnStartup = false,
    ShowStandaloneDocumentCommands = false,
    ToolbarPlacement = FlowDesignerToolbarPlacement.External
});

hostToolbar.Children.Add(designer.ToolbarView);
await designer.ResetDocumentAsync("strategy-001", "策略连线图");
await designer.LoadDocumentAsync(existingDocument);
var snapshot = designer.CaptureDocument();
var publishResult = designer.PublishRuntimeFile(@"C:\Flows\strategy-001.flowruntime");
```

- `LoadDocumentAsync` 会停止当前调试、切回编辑模式并加载传入文档的深拷贝。
- `ResetDocumentAsync` 创建不含示例节点的空白图，并使设计态和运行态使用相同的 `FlowId` / `FlowName`。
- `CaptureDocument` 先同步已渲染节点坐标、缩放、画布尺寸和滚动偏移，再返回通过 `FlowDesignSerializer` 生成的深拷贝。
- `PublishRuntimeFile` 只允许在编辑模式调用。它捕获当前文档，通过 `FlowPublishService` 完成 Schema v2 深拷贝和校验，并仅在成功时写入 `.flowruntime`；失败原因从返回值的 `Validation` 获取。
- 宿主持有的输入文档和捕获结果都不会与控件内部文档共享可变对象。
- `FlowDesignerOptions.ToolbarPlacement` 默认为 `Internal`。设为 `External` 后，`ToolbarView` 不再挂在设计器内部，宿主必须把这个单例元素放入自己的单层命令栏；关闭独立文件命令时，四个中文模式/运行命令可在 300 px 分配宽度内完成布局。

独立设计器工具栏的 Publish 按钮调用同一个 `PublishRuntimeFile` 入口，不另行维护 UI 专用发布逻辑。

## 属性面板

属性面板根据 `NodeSettingDescriptor` 动态生成编辑器。输入端口只用于控制流连线，不生成独立的 `Input Bindings` 编辑区。

选中节点后，属性面板编辑的是节点名称、`Settings` 和 `ExecutionPolicy` 的深拷贝草稿，而不是源文档：

- “应用”先完成必填、数字转换、动态候选、变量来源/类型和执行策略校验，再一次性写回源节点；校验失败会保留原始文本和草稿，并聚焦首个错误控件。
- 每个可校验编辑器都预留固定高度的错误槽，错误出现、消失或变量状态变为有效时，当前输入控件和后续表单项的坐标保持不变，不会发生上下跳动。
- “重置”恢复到最近一次成功应用的基线；没有变化时两个按钮禁用。
- `HasPendingPropertyChanges` 报告有效修改和非法原始文本；`TryApplyPendingPropertyChanges(out string error)`、`DiscardPendingPropertyChanges()` 和 `TryResolvePendingPropertyChanges()` 供宿主协调保存、导航和关闭。
- 加载或重置草稿时已经存在的候选失效、必填缺失等基线校验错误不视为新的未应用修改；用户后续输入和候选变化仍会进入 pending 状态。放弃修改会重新建立该基线，宿主后续捕获或切换文档不会重复提示。
- `FlowDesignerOptions.PendingPropertyChangesPrompt` 可以返回 `Apply`、`Discard` 或 `Cancel`。为空时使用 SDK 自绘三按钮确认框；测试宿主可注入确定性返回值。
- 切换节点、进入调试、新建、加载、打开、保存、发布、删除当前节点和关闭 Demo 前都会先解决草稿。取消或应用失败时停留在当前上下文，不会静默丢失输入。
- 调试运行模式以只读提示替代“应用 / 重置”按钮，并禁用全部属性编辑器。

配置项声明为 `ConstantOrVariable` 后，会在同一行提供“固定值 / 变量”切换：

- 普通固定值使用固定 40 px、单行不换行的现代文本框完成字符串、数字和日期转换，长内容在控件内水平浏览，不会撑高表单；只有既有 `Mappings` / `Channels` 结构化字段保留换行、回车输入和纵向滚动。Boolean 使用绿色开关；设计器不会根据配置键名称猜测下拉选项。
- 变量模式用结构化 `VariableSelector` 替换整个配置值；切换期间保留原 `ConstantValue`，切回固定值时恢复。
- 节点输出候选只来自当前节点沿控制入边反向遍历得到的全部直接、间接前置节点，不显示自身、下游或无关节点。
- 候选项显示节点名称、节点 ID、输出名称和类型，并只保留与目标配置项兼容的变量；当设置或输出声明 `EnumType` 时还必须是同一个具体枚举类型。数值扩宽、不同枚举之间转换和字符串隐式转换均不开放。
- `Object` 输出声明 `ObjectType` 后，菜单中的对象项本身仍可点击选择；鼠标移到该项会展开公开可读属性和公开字段组成的首层子菜单，子项也可选择，但不会继续展开第二层。类型化 Object 按 CLR 可赋值关系过滤；无实际类型的 Object 来源不会出现在类型化目标候选中。
- Token 字段单独分组。变量来源因删除节点、删除连线或 Descriptor 变化而失效时，选择器保留原 Selector 并显示错误，不会静默清空。
- 环境变量单独分组，显示名称、稳定 Id 和类型，不受控制流拓扑限制；定义删除或类型变化导致绑定失效时同样保留原 Selector 并显示错误。
- Session 全局变量使用独立“全局变量”分组，按稳定 Id 保存、按 `FlowDataType` 精确过滤；宿主通过 `UpdateGlobalVariables` 同步当前流程定义。
- 嵌入式宿主在配方变量变化后调用 `UpdateEnvironmentVariables`，设计器会原位更新流程定义和候选并保留当前属性草稿。
- `ConstantOnly` 或 `ListenerStart` 配置不开放执行期节点输出变量；只读模式同时禁用模式切换、固定值编辑器和变量选择器。

`NodeSettingEditorKind.VariableSelectorMappings` 使用专用表格式编辑器。每行编辑稳定
Attribute 名称和一个结构化变量来源，并支持新增、删除及排序；空名称、大小写不敏感
重复名称、未选择来源、来源超出 Descriptor 范围或当前候选失效都会禁止应用。

节点卡片只摘要显示变量模式的配置来源，不再摘要控制输入端口绑定。

具体项目可以通过传入自己的节点注册表、节点 Descriptor 和调试设备来扩展属性面板的实际体验。

嵌入式宿主还可以通过 `FlowDesignerOptions.SettingConstantOptionsProvider` 为固定值编辑器提供明确的动态候选项。例如项目相机节点的 `CameraId` 可以直接读取宿主当前绑定的设备配置，并在 Descriptor 中声明为 `ConstantOnly`，从而只显示设备数据源下拉框，不显示固定值/变量切换。候选项发生变化后调用 `RefreshSelectedNodeProperties()` 即可刷新当前属性面板。刷新同一节点会保留设置和执行策略的草稿、非法原始文本及行内错误；当前值从候选中失效时继续显示原值并禁止应用，不会清空用户输入。宿主为该 Descriptor 返回非 `null` 候选集合时，编辑器使用不可自由输入的现代下拉框；即使集合为空也保持空下拉框。宿主返回 `null` 时，声明了有效 `EnumType` 的设置使用枚举成员下拉框，其余设置继续使用手工输入控件。设计器不再为相机标识提供硬编码默认值。

对于支持“固定值 / 变量”切换的配置项，两个 40 px 圆角分段按钮之间保留明确间距，并与右侧 40 px 固定值编辑器或变量选择器顶边对齐。状态或校验提示显示在控件下方的固定错误槽内，红色描边只覆盖编辑器本身，不改变输入控件及相邻表单项的布局。

配置项声明 `Validator` 后，普通文本框、Boolean 开关和宿主候选下拉都会在常量
完成 `DataType` 转换后执行同一单项规则。错误显示在当前编辑器的固定错误槽中并
禁用“应用”；初始化、重置、候选刷新和动态 Descriptor 切换同样会重新校验。
Validator 只处理设计期常量，不读取其他配置项，也不在调试或生产运行时校验
变量的实际值。

### 实例级动态 Descriptor

节点工厂实现 `IInstanceNodeDescriptorProvider` 后，Designer 会通过 `NodeRegistry.ResolveDescriptor(NodeDefinition)` 为每个节点实例解析当前生效的端口、配置项和输出。需要读取全局变量定义等流程元数据时实现 `IFlowDefinitionNodeDescriptorProvider`，Designer 调用 `ResolveDescriptor(RuntimeFlowDefinition, NodeDefinition)` 并优先使用流程感知结果。节点库和新建节点默认值仍使用工厂的静态 `Descriptor`；画布节点卡片、属性草稿、执行策略回退输出和上游变量候选使用实例 Descriptor。

用于切换 Descriptor 结构的配置项必须声明 `AffectsDescriptor = true`，并使用 `ConstantOnly`。该固定值在属性草稿中变化时，Designer 会立即刷新属性面板且不提前写回源文档：

- 移除旧 Descriptor 独有的 Setting，并按新 Descriptor 的默认值补齐新增 Setting。
- 同名且数据类型、绑定模式、求值阶段和变量来源契约一致的 Setting/Selector 保留原值。
- `DefaultOutputs` 移除已不存在或类型已变化的输出；失败策略为 `DefaultOutputs` 时，为新增且可创建常量的输出补齐类型默认值。
- 只清理已移除字段对应的原始文本和行内错误；无关字段的非法输入仍保留。
- 下游节点引用已移除输出的 `VariableSelector` 不会被静默改写或删除，发布校验会报告失效来源。

动态 Descriptor 解析异常时，Designer 临时回退静态 Descriptor 以保持文档可编辑；`FlowValidator` 使用 `NodeDescriptorResolutionFailed` 报告结构化错误并阻止发布。Descriptor 仍是派生信息，不进入 `.flowdesign` 或 `.flowruntime`。

### 节点执行策略

所有节点都显示独立的“执行策略”静态编辑区。它属于引擎控制面，不是节点业务配置，因此不提供“固定值 / 变量”切换，也不会创建 `VariableSelector`：

- `TimeoutMs` 配置单次执行超时，`0` 表示继承流程全局超时；`MaxConcurrentExecutions` 限制同一节点实例的最大并发执行数。
- 重试采用 Dify 风格的简化界面，默认关闭。开启后只编辑最大重试次数 `MaxRetries` 和固定重试间隔 `RetryIntervalMs`；节点卡片同步显示“重试 N 次 · M ms”中文摘要，关闭后隐藏摘要。
- `StopFlow` 表示最终失败后停止本次运行；`ErrorBranch` 表示沿 `Error` 或 `Timeout` 控制端口继续，没有对应连线时本次流程失败。
- `DefaultOutputs` 根据 `NodeDescriptor.Outputs` 生成常量编辑器。String、Int32、Int64、Double、Boolean、DateTime 和 Object 会在写入 `NodeExecutionPolicy.DefaultOutputs` 前完成类型转换；Control、IVisionImage 和 CameraFrameData 等不能由界面创建的运行时对象会明确提示不支持。
- 切换失败策略时保留已经填写的回退常量；调试运行只读模式会同时禁用超时、并发、重试、失败策略和回退值编辑器。

## 画布缩放

鼠标滚轮缩放以当前鼠标指针为锚点：缩放前后，指针下方对应的画布逻辑坐标保持不变。工具栏的缩放按钮以当前可见视口中心为锚点。节点卡片文字使用适合几何缩放的字形度量，并在倍率变化后重新布局和绘制，避免缩放过程中复用模糊的文字渲染结果。

节点阴影由不包含文字内容的独立背景层绘制，避免 WPF 为阴影效果把整张卡片及文字预先栅格化。节点卡片在 75%（含接近该值的滚轮缩放档位）使用 `Display` 字形度量、ClearType 和固定像素提示，使该常用缩放比例下的标题、说明和摘要文字优先保持清晰；其它倍率继续使用适合几何缩放的 `Ideal` 度量。

画布左下角显示整个逻辑画布的缩略图，包含节点、连线和蓝色当前视野框。按住视野框拖动会同步平移主画布；在缩略图其它位置按下则将视野中心快速移动到该位置。视野框在不同缩放倍率下使用逻辑画布坐标计算，并始终限制在画布范围内。

主画布按住空白区域平移时，滚动偏移会对齐到当前 DPI 的物理像素网格，避免节点描边和阴影在连续帧间落到不同的子像素位置而产生边缘割裂。输入、输出端口使用位于卡片左右描边外侧的蓝色短条视觉，同时保留较大的透明鼠标命中区域。控制流方向由左右端口位置表达，贝塞尔曲线直接收束到输入端口中心，不额外绘制悬空的三角箭头。

平移和滚轮缩放使用 WPF 合成帧合并高频输入：同一显示帧内的多次鼠标移动只保留最新滚动位置，多次滚轮输入累积为一个缩放目标，每帧最多执行一次滚动与一次画布重排。节点文字不进入位图缓存，独立的纯背景阴影层使用 `BitmapCache` 复用栅格结果，从而在保持文字清晰的同时减少平移和缩放期间的重复绘制。

## 调试运行

Designer 调试运行会把当前 `.flowdesign` 发布为运行态定义，再通过同一个 `FlowRunner` 执行，并订阅 `FlowRuntimeEvent` 高亮节点和显示日志。

运行调试区使用三态偏好：默认 `Auto` 在编辑模式收起、进入调试模式自动展开；用户手动展开后以 `Open` 跨模式保持，手动收起后以 `Closed` 保持。无论用户偏好如何，收到 `NodeFailed` 或 `NodeTimeout` 都会立即展开以显示诊断信息，但不会改写用户的长期偏好。

切换到“调试运行”模式后，右侧会显示入口面板：

- 入口下拉框列出 `RuntimeFlowDefinition.Entries`，并显示 Manual、External 或 NodeEvent 类型。
- Manual 入口根据 `TriggerInputDescriptor` 生成临时输入表单，支持 String、Int32、Int64、Double、Boolean、DateTime 和 Object；必填、默认值和类型转换在触发前校验。
- 点击 Debug Run 时，设计器以所选入口、表单输入和调试 Token 创建 `FlowTriggerRequest`。表单值只服务于当前调试会话，不写入 `.flowdesign` 或 `.flowruntime`。
- External 入口只展示外部宿主触发说明和输入协议；NodeEvent 入口额外展示监听源节点，两者不能由设计器伪装成手动来源触发。
- 调试运行期间入口选择和输入表单只读，运行结果根据 `FlowRunResult` 显示成功、失败、取消或拒绝状态。
- 节点卡片区分普通运行状态与执行策略事件：`NodeRetrying` 显示“重试中”和下一次尝试序号，`NodeRecovered` 显示“已恢复”，`NodeCancelled` 显示“已取消”，`NodeSkipped` 显示“已跳过”。恢复事件后的 `NodeCompleted` 不会覆盖“已恢复”结果；运行状态摘要与卡片内的“已启用重试”配置摘要同时保留。

配置项选择 TriggerInput 变量时，候选只来自能够到达当前节点的入口输入。多个可达入口中同名同类型的输入合并为一个“触发输入”候选；同名但类型不同的输入不进入候选，并在属性面板显示冲突。

生产进程必须使用 `.flowruntime`，不依赖 Designer 控件、画布或 ViewModel。

## 枚举编辑体验

Designer 根据 `FlowDataType` 选择属性编辑控件：`Boolean` 使用绿色开关，`Int32` / `Double` 使用手工输入及数字文本转换，其它普通类型使用手工输入文本框。`NodeSettingDescriptor.EnumType` 指向有效枚举类型时，设计器自动使用枚举成员下拉框；宿主也可以通过 `SettingConstantOptionsProvider` 为具体 Descriptor 提供优先级更高的明确选项数据源。

端口连线规则使用 `FlowPortDirection` 判断输入/输出方向。条件操作符、AND Join 重复策略和日志等级仍写回既有字符串协议值，并由既有校验器检查；若宿主希望把这些值限制为下拉候选，应通过 `SettingConstantOptionsProvider` 返回对应 wire values，不修改 `.flowdesign` / `.flowruntime` 协议。
