# AGENTS.md

## 项目概述

本仓库是独立工业视觉流程 SDK：`VisionFlowSdk`�?

当前 SDK 只保留：

- UI 无关的流程执行引擎�?
- 流程定义、运行态模型、变量池、运行事件、校验、发布和序列化�?
- Adapter 契约和运行时扩展服务�?
- Core 基础流程节点�?
- WPF 流程设计器�?
- WinForms / WPF Demo�?
- 测试项目�?

设备节点、算法节点、图像保存、数据库保存、拼图、扫描和融合节点不再放在 SDK 内，应由具体项目或项目专属节点库实现�?

生产 WinForms 上位机必须加�?`.flowruntime` 并通过 `FlowRunner` 执行，不打开 WPF 流程设计器�?

---

## 解决方案结构

```text
src/Vision.Flow.Core
src/Vision.Flow.Designer.Wpf

tests/Vision.Flow.Tests

demos/Vision.Flow.Demo.WinForms
demos/Vision.Flow.Demo.DesignerWpf

samples/flows
docs
build
```

---

## 项目职责

### Vision.Flow.Core

包含�?

- 流程定义
- 运行态定�?
- 节点接口
- 执行引擎
- Token
- 变量�?
- 运行事件
- 流程校验
- 流程序列�?
- 设备适配器接�?
- Core 基础节点

Core 内置节点只包含：

```text
delay.wait
log.write
variable.set
flow.split
join.and
condition.if
```

这些节点源码命名空间保留�?`Vision.Flow.Nodes`，但编译产物属于 `Vision.Flow.Core.dll`�?

允许�?

- �?C# 模型
- 运行时逻辑
- Adapter 接口
- JSON 序列�?
- 校验逻辑
- 运行时事�?
- 基础流程节点

禁止�?

- WPF 引用
- WinForms 引用
- 具体相机 SDK 引用
- 具体光源 SDK 引用
- 具体运控 SDK 引用
- 上位机业务代�?
- 项目专属设备/算法节点实现

### Vision.Flow.Designer.Wpf

包含 WPF 流程设计器�?

允许�?

- WPF 控件
- 流程画布
- 节点卡片
- 端口和连�?
- 动态属性面�?
- 变量选择�?
- 调试面板
- `.flowdesign` 保存/加载/发布 UI

禁止�?

- 承担生产运行逻辑
- 直接调用设备 SDK
- 绕过 Adapter 操作设备

Designer 默认注册 Core 基础节点；具体项目可以注入自己的 `NodeRegistry` 来提供设�?算法节点�?

### Vision.Flow.Tests

包含单元测试和集成测试�?

修改 Runtime、Node、Serialization、Compiler 时必须增加或更新测试�?

---

## 目标框架和语言

- 目标框架�?NET Framework 4.8�?
- UI：WPF 设计器、WinForms Demo�?
- 尽量使用兼容 .NET Framework 4.8 �?C# 写法�?
- 使用 `async` / `await` 处理异步逻辑�?
- 不阻�?UI 线程�?
- 不在相机 SDK 回调线程执行重算法�?

---

## 依赖规则

允许依赖�?

```text
Designer.Wpf -> Flow.Core

Demo.WinForms -> Flow.Core

Demo.DesignerWpf -> Flow.Core
Demo.DesignerWpf -> Designer.Wpf

Tests -> Flow.Core
Tests -> Designer.Wpf

ProjectSpecificNodes -> Flow.Core
```

禁止依赖�?

```text
Flow.Core -> WPF
Flow.Core -> WinForms
Flow.Core -> specific SDK

Runtime -> Designer UI
Production Runtime -> WPF Canvas
```

---

## 运行时规�?

生产运行时必须：

- 加载 `.flowruntime`�?
- 创建 `FlowRunner`�?
- 注册 Core 基础节点工厂�?
- 注册项目专属节点工厂�?
- 从外部事件触发流程入口�?
- 发布 `FlowRuntimeEvent`�?
- 不打开 WPF 流程设计器�?
- 不依赖节点卡片、画布状态、Designer ViewModel�?

WPF 调试运行可以�?

- 将当�?`.flowdesign` 编译为运行态定义�?
- 通过同一�?`FlowRunner` 调试运行�?
- 订阅 `FlowRuntimeEvent`�?
- 高亮节点和显示调试输出�?

---

## 流程文件规则

使用两类流程文件�?

```text
.flowdesign
  设计态文件�?
  包含运行态流程和设计器视图状态�?

.flowruntime
  生产运行态文件�?
  只包含运行时节点、连线、入口、配置和变量绑定�?
```

`.flowruntime` 禁止包含�?

- 节点 X/Y 坐标
- Canvas 缩放
- WPF 样式
- Debug-only UI 状�?
- WPF 类型�?

---

## 节点开发规�?

每个新节点必须包含：

- Config �?
- Runtime Node 类，实现 `IFlowNode`
- NodeFactory
- NodeDescriptor
- 输入/输出端口定义
- 输出变量定义
- 单元测试或集成测�?
- 必要时更新项目自己的示例流程

项目专属节点类型建议加项目或领域前缀，例如：

```text
station.camera.soft_trigger
station.recipe.run
station.image.save
station.database.save
station.fusion.final
```

节点必须只通过 `Vision.Flow.Core` 中定义的 Adapter 接口或项目自己的兼容契约访问设备�?

正确做法�?

```csharp
var camera = context.Devices.GetCamera(cameraId);
await camera.SoftTriggerAsync(triggerContext, cancellationToken);
```

错误做法�?

```csharp
var camera = new HikCamera();
camera.MV_CC_SetCommandValue_NET(...);
```

---

## 设备适配器规�?

Adapter 包装现有上位机代码、真实设�?SDK 或测试桩�?

Adapter 接口放在 `Flow.Core`�?

真实设备集成、Fake Adapter、Demo 设备和项目专属节点应由具体上位机应用或单独项目实现，不放�?`Flow.Core` �?`Designer.Wpf`�?

---

## UI 规则

WPF 设计器应提供现代节点编辑体验�?

- 左侧节点库�?
- 中间无限画布�?
- 右侧动态属性面板�?
- 底部调试面板�?
- 节点卡片�?
- 贝塞尔连线�?
- 缩放和平移�?
- 拖拽创建节点�?
- 拖拽端口连接节点�?
- 运行态节点高亮�?
- 变量选择器用于节点输入绑定�?

节点卡片只展示摘要，不展示全部配置。详细配置放右侧属性面板�?

---

## 变量绑定规则

执行线表示控制流�?

数据主要通过变量传递：

```text
{{ set_result.Value }}
{{ token.TokenId }}
```

变量选择器应尽量按数据类型过滤�?

---

## 线程规则

- 不在 SDK 回调线程执行重算法�?
- 相机回调应快速封装帧数据并返回�?
- 长耗时任务使用异步任务或有界队列�?
- 不阻�?WPF UI 线程�?
- 异步 API 使用 `CancellationToken`�?
- 设备操作应支持超时�?

---

## 测试规则

修改执行引擎时：

- 增加或更�?Runtime 测试�?
- 验证节点顺序�?
- 验证错误事件�?
- 验证取消或超时逻辑�?

修改 Core 基础节点时：

- 增加或更新节点测试�?
- 验证输出变量和运行事件�?

修改项目专属节点时：

- 在对应项目增加测试�?
- 使用项目自己�?Fake Adapter 或测试桩�?

修改序列化时�?

- 增加 round-trip 测试�?
- 确保 `.flowruntime` 不包�?WPF view state�?

修改设计�?UI 时：

- 确保解决方案仍能编译�?
- 确保 Designer Demo 可打开�?
- 确保保存和发布仍正常�?

---

## 构建和测试命�?

优先使用脚本�?

```powershell
./build/build.ps1
./build/test.ps1
./build/clean.ps1
```

---

## 文档更新规则

行为变化时更新文档：

- 架构变化 -> `docs/01-architecture.md`
- 流程文件变化 -> `docs/02-flow-file-format.md`
- Runtime 变化 -> `docs/03-runtime-design.md`
- 新节点规�?-> `docs/04-node-development-guide.md`
- Adapter 变化 -> `docs/05-device-adapter-guide.md`
- Designer UI 变化 -> `docs/06-designer-ui-guide.md`
- 测试策略变化 -> `docs/07-test-plan.md`
- 集成方式变化 -> `docs/09-release-and-integration.md`

---

## 代码组织与注释准�?
- 大而全的代码文件应按职责进行拆分，同类型代码应归置到明确的文件夹中�?- 新增注释必须使用中文�?- 注释说明职责边界、复杂流程、线�?队列约束、设备适配边界和不明显的业务规则�?
- 注释不应逐行复述代码�?
- 公共模型类和关键属性应补充中文注释，说明用途、运行�?设计态边界、序列化影响、上位机集成含义和兼容性约束�?
- 节点类型、端口名、数据类型、变量输出名、设置键、事�?Data 键、校验错误码、文件扩展名等领域字符串应集中定义�?

---

## Git 提交与分支规�?
- 每个功能点完成后，必须提交当前更改�?- 如果使用功能分支开发，功能分支提交完成后必须合并回主分支�?- 合并回主分支后必须确认构建、测试或相关验证无误�?- 合并确认无误后，必须删除已合并的功能分支�?
---

## 完成标准

任务完成前必须满足：

- 代码能编译�?
- 相关测试通过，或者明确说明失败原因�?
- 公共 API 足够清晰，便于上位机集成�?
- 不引入禁止的项目引用�?
- 不破�?Demo�?
- 新节点包�?Descriptor 和测试�?

---

## Review 检查项

- 依赖方向�?
- UI / Runtime 分离�?
- Node / Adapter 分离�?
- 线程和取消逻辑�?
- 序列化兼容性�?
- 测试覆盖�?
- 生产运行是否仍可�?WPF 执行�?
