# 06 - Designer UI Guide

## 目标

WPF Designer 提供流程编辑、调试和发布体验，但不承担生产运行逻辑。

## 主要区域

```text
top toolbar: edit / debug mode / new / sample / open / save / publish / debug run / stop
left: node palette
center: canvas
right: property panel
bottom: runtime debug panel
```

`FlowDesignerOptions.ShowStandaloneDocumentCommands` 默认为 `true`，保持独立设计器原有的 New / Sample / Open / Save / Publish 命令。业务宿主统一管理自己的复合配置文件时可设为 `false`；此时仍保留编辑模式、调试运行模式、Debug Run 和 Stop。

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
    ShowStandaloneDocumentCommands = false
});

await designer.ResetDocumentAsync("strategy-001", "策略连线图");
await designer.LoadDocumentAsync(existingDocument);
var snapshot = designer.CaptureDocument();
```

- `LoadDocumentAsync` 会停止当前调试、切回编辑模式并加载传入文档的深拷贝。
- `ResetDocumentAsync` 创建不含示例节点的空白图，并使设计态和运行态使用相同的 `FlowId` / `FlowName`。
- `CaptureDocument` 先同步已渲染节点坐标、缩放、画布尺寸和滚动偏移，再返回通过 `FlowDesignSerializer` 生成的深拷贝。
- 宿主持有的输入文档和捕获结果都不会与控件内部文档共享可变对象。

## 属性面板

属性面板根据 `NodeSettingDescriptor` 动态生成编辑器。输入端口只用于控制流连线，不生成独立的 `Input Bindings` 编辑区。

配置项声明为 `ConstantOrVariable` 后，会在同一行提供“固定值 / 变量”切换：

- 固定值模式继续使用文本、数字、复选框或枚举编辑器。
- 变量模式用结构化 `VariableSelector` 替换整个配置值；切换期间保留原 `ConstantValue`，切回固定值时恢复。
- 节点输出候选只来自当前节点沿控制入边反向遍历得到的全部直接、间接前置节点，不显示自身、下游或无关节点。
- 候选项显示节点名称、节点 ID、输出名称和 `FlowDataType`，并按目标配置类型过滤；`Object` 到具体类型的转换会显示风险提示。
- Token 字段单独分组。变量来源因删除节点、删除连线或 Descriptor 变化而失效时，选择器保留原 Selector 并显示错误，不会静默清空。
- `ConstantOnly` 或 `ListenerStart` 配置不开放执行期节点输出变量；只读模式同时禁用模式切换、固定值编辑器和变量选择器。

节点卡片只摘要显示变量模式的配置来源，不再摘要控制输入端口绑定。

具体项目可以通过传入自己的节点注册表、节点 Descriptor 和调试设备来扩展属性面板的实际体验。

## 画布缩放

鼠标滚轮缩放以当前鼠标指针为锚点：缩放前后，指针下方对应的画布逻辑坐标保持不变。工具栏的缩放按钮以当前可见视口中心为锚点。节点卡片文字使用适合几何缩放的字形度量，并在倍率变化后重新布局和绘制，避免缩放过程中复用模糊的文字渲染结果。

## 调试运行

Designer 调试运行会把当前 `.flowdesign` 发布为运行态定义，再通过同一个 `FlowRunner` 执行，并订阅 `FlowRuntimeEvent` 高亮节点和显示日志。

切换到“调试运行”模式后，右侧会显示入口面板：

- 入口下拉框列出 `RuntimeFlowDefinition.Entries`，并显示 Manual、External 或 NodeEvent 类型。
- Manual 入口根据 `TriggerInputDescriptor` 生成临时输入表单，支持 String、Int32、Int64、Double、Boolean、DateTime 和 Object；必填、默认值和类型转换在触发前校验。
- 点击 Debug Run 时，设计器以所选入口、表单输入和调试 Token 创建 `FlowTriggerRequest`。表单值只服务于当前调试会话，不写入 `.flowdesign` 或 `.flowruntime`。
- External 入口只展示外部宿主触发说明和输入协议；NodeEvent 入口额外展示监听源节点，两者不能由设计器伪装成手动来源触发。
- 调试运行期间入口选择和输入表单只读，运行结果根据 `FlowRunResult` 显示成功、失败、取消或拒绝状态。

配置项选择 TriggerInput 变量时，候选只来自能够到达当前节点的入口输入。多个可达入口中同名同类型的输入合并为一个“触发输入”候选；同名但类型不同的输入不进入候选，并在属性面板显示冲突。

生产进程必须使用 `.flowruntime`，不依赖 Designer 控件、画布或 ViewModel。

## 枚举编辑体验

Designer 根据 `FlowDataType` 选择属性编辑控件：`Boolean` 使用复选框，`Int32` / `Double` 使用数字文本转换，其它类型使用文本或下拉框。

端口连线规则使用 `FlowPortDirection` 判断输入/输出方向。条件操作符、AND Join 重复策略和日志等级的下拉项由 `FlowEnumConverter.GetWireValues<TEnum>()` 生成，并写回字符串协议值，保证保存后的 `.flowdesign` / `.flowruntime` 仍然可读。
