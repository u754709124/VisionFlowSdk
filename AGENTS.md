# AGENTS.md

## 项目概述

本仓库是独立工业视觉流程 SDK：`VisionFlowSdk`。

SDK 提供：

- UI 无关的流程执行引擎。
- 工业视觉公共节点。
- 设备适配器接口，用于复用现有上位机设备、算法、存储逻辑。
- Fake Adapter，用于测试、Demo 和离线 UI 调试。
- WPF 流程设计器，用于编辑、调试和发布流程。
- WinForms 与 WPF Demo 项目。
- 测试项目。

最终 WinForms 上位机会直接引用生成的 DLL。生产运行时必须加载 `.flowruntime` 并通过 `FlowRunner` 执行，不打开 WPF 流程设计器。

---

## 解决方案结构

期望结构：

```text
src/Vision.Flow.Core
src/Vision.Flow.Nodes
src/Vision.DeviceAdapters
src/Vision.Flow.Designer.Wpf

tests/Vision.Flow.Tests

demos/Vision.Flow.Demo.WinForms
demos/Vision.Flow.Demo.DesignerWpf
```

---

## 项目职责

### Vision.Flow.Core

包含：

- 流程定义
- 运行态定义
- 节点接口
- 执行引擎
- Token
- 变量池
- 运行事件
- 流程校验
- 流程序列化
- 设备适配器接口

允许：

- 纯 C# 模型
- 运行时逻辑
- Adapter 接口
- JSON 序列化
- 校验逻辑
- 运行时事件

禁止：

- WPF 引用
- WinForms 引用
- 具体相机 SDK 引用
- 具体光源 SDK 引用
- 具体运控 SDK 引用
- 上位机业务代码

### Vision.Flow.Nodes

包含公共节点运行实现。

允许：

- Runtime Node
- Node Config
- Node Factory
- Node Descriptor
- 工业视觉公共节点

禁止：

- WPF 引用
- WinForms 引用
- 具体设备 SDK 引用
- 直接调用旧设备类

节点必须只通过 `Vision.Flow.Core` 中定义的 Adapter 接口访问设备。

### Vision.DeviceAdapters

包含：

- 默认设备注册表
- FakeCameraAdapter
- FakeLightAdapter
- FakeMotionAdapter
- FakeRecipeAdapter
- FakeImageSaveAdapter
- FakeDatabaseAdapter
- 适配器基类或辅助类

不要把真实生产上位机业务逻辑放在这里，除非它是 Demo-only 或通用模拟逻辑。

### Vision.Flow.Designer.Wpf

包含 WPF 流程设计器。

允许：

- WPF 控件
- 流程画布
- 节点卡片
- 端口和连线
- 动态属性面板
- 变量选择器
- 调试面板
- `.flowdesign` 保存/加载/发布 UI

禁止：

- 承担生产运行逻辑
- 直接调用设备 SDK
- 绕过 Adapter 操作设备

### Vision.Flow.Tests

包含单元测试和集成测试。

修改 Runtime、Node、Serialization、Compiler 时必须增加或更新测试。

---

## 目标框架和语言

- 目标框架：.NET Framework 4.8。
- UI：WPF 设计器、WinForms Demo。
- 尽量使用兼容 .NET Framework 4.8 的 C# 写法。
- 使用 `async` / `await` 处理异步逻辑。
- 不阻塞 UI 线程。
- 不在相机 SDK 回调线程执行重算法。

---

## 依赖规则

允许依赖：

```text
Designer.Wpf -> Flow.Core
Designer.Wpf -> Flow.Nodes

Flow.Nodes -> Flow.Core

DeviceAdapters -> Flow.Core

Demo.WinForms -> Flow.Core
Demo.WinForms -> Flow.Nodes
Demo.WinForms -> DeviceAdapters

Demo.DesignerWpf -> Flow.Core
Demo.DesignerWpf -> Flow.Nodes
Demo.DesignerWpf -> DeviceAdapters
Demo.DesignerWpf -> Designer.Wpf
```

禁止依赖：

```text
Flow.Core -> WPF
Flow.Core -> WinForms
Flow.Core -> specific SDK

Flow.Nodes -> WPF
Flow.Nodes -> WinForms
Flow.Nodes -> specific SDK

Runtime -> Designer UI
Production Runtime -> WPF Canvas
```

---

## 运行时规则

生产运行时必须：

- 加载 `.flowruntime`。
- 创建 `FlowRunner`。
- 注册节点工厂。
- 注册设备适配器。
- 从外部事件触发流程入口。
- 发布 `FlowRuntimeEvent`。
- 不打开 WPF 流程设计器。
- 不依赖节点卡片、画布状态、Designer ViewModel。

WPF 调试运行可以：

- 将当前 `.flowdesign` 编译为运行态定义。
- 通过同一个 `FlowRunner` 调试运行。
- 订阅 `FlowRuntimeEvent`。
- 高亮节点和显示调试输出。

---

## 流程文件规则

使用两类流程文件：

```text
.flowdesign
  设计态文件。
  包含运行态流程和设计器视图状态。

.flowruntime
  生产运行态文件。
  只包含运行时节点、连线、入口、配置和变量绑定。
```

`.flowruntime` 禁止包含：

- 节点 X/Y 坐标
- Canvas 缩放
- WPF 样式
- Debug-only UI 状态
- WPF 类型名

---

## 节点开发规则

每个新节点必须包含：

- Config 类
- Runtime Node 类，实现 `IFlowNode`
- NodeFactory
- NodeDescriptor
- 输入/输出端口定义
- 输出变量定义
- 单元测试或集成测试
- 必要时更新 Demo 示例流程

命名约定：

```text
CameraSoftTriggerNode
CameraSoftTriggerNodeConfig
CameraSoftTriggerNodeFactory
```

NodeType 命名约定：

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

正确做法：

```csharp
var camera = context.Devices.GetCamera(cameraId);
await camera.SoftTriggerAsync(...);
```

错误做法：

```csharp
var camera = new HikCamera();
camera.MV_CC_SetCommandValue_NET(...);
```

---

## 设备适配器规则

Adapter 包装现有上位机代码或 Fake Demo 设备。

真实设备集成应该由上位机应用实现，或者放到单独的适配器项目里，不放进 `Flow.Core` 或 `Flow.Nodes`。

Adapter 接口放在 `Flow.Core`。

Fake Adapter 放在 `Vision.DeviceAdapters`。

---

## 相机节点规则

相机设备支持至少包含以下节点：

- `camera.set_parameters`
- `camera.soft_trigger`
- `camera.image_callback`

### camera.set_parameters

必须：

- 根据 `CameraId` 选择相机设备。
- 支持一个或多个参数设置。
- 支持常量值和变量绑定值。
- 通过 `ICameraAdapter` 执行。

### camera.soft_trigger

必须：

- 根据 `CameraId` 选择相机设备。
- 创建或传递 `TriggerId`。
- 调用 `ICameraAdapter.SoftTriggerAsync`。
- 不直接等待图像，除非明确设计为同步触发采图节点。

### camera.image_callback

必须：

- 根据 `CameraId` 选择相机设备。
- 支持等待下一张匹配图像。
- 后续支持外触发连续流模式。
- 输出变量包括：`Image`、`Frame`、`FrameId`、`GrabTime`、`Metadata`。

---

## 工业视觉场景

SDK 长期需要支持：

1. 单拍检测：
   运控到位 -> 光源 -> 相机 -> 算法 -> 图片保存 -> 数据库保存。

2. 多点位图像组和拼图：
   点位 1 拍照 -> 点位 2 拍照 -> 图像组汇合 -> 拼图 -> 算法。

3. 连续扫描：
   运控到位 -> 相机外触发流 -> 单帧预处理 -> 扫描组汇合 -> 最终 3D/2D 融合 -> 算法。

---

## UI 规则

WPF 设计器应提供现代节点编辑体验：

- 左侧节点库。
- 中间无限画布。
- 右侧动态属性面板。
- 底部调试面板。
- 节点卡片。
- 贝塞尔连线。
- 缩放和平移。
- 拖拽创建节点。
- 拖拽端口连接节点。
- 运行态节点高亮。
- 变量选择器用于节点输入绑定。

节点卡片只展示摘要，不展示全部配置。详细配置放右侧属性面板。

---

## 变量绑定规则

执行线表示控制流。

数据主要通过变量传递：

```text
{{ camera_callback_1.Image }}
{{ recipe_1.Result }}
{{ fusion_1.Final3DImage }}
```

变量选择器应尽量按数据类型过滤。

---

## 线程规则

- 不在 SDK 回调线程执行重算法。
- 相机回调应快速封装帧数据并返回。
- 长耗时任务使用异步任务或有界队列。
- 不阻塞 WPF UI 线程。
- 异步 API 使用 `CancellationToken`。
- 设备操作应支持超时。

---

## 测试规则

修改执行引擎时：

- 增加或更新 Runtime 测试。
- 验证节点顺序。
- 验证错误事件。
- 验证取消或超时逻辑。

修改节点时：

- 增加或更新节点测试。
- 使用 Fake Adapter。
- 验证输出变量和运行事件。

修改序列化时：

- 增加 round-trip 测试。
- 确保 `.flowruntime` 不包含 WPF view state。

修改设计器 UI 时：

- 确保解决方案仍能编译。
- 确保 Designer Demo 可打开。
- 确保保存和发布仍正常。

---

## 构建和测试命令

优先使用脚本：

```powershell
./build/build.ps1
./build/test.ps1
./build/clean.ps1
```

如果脚本不存在，请创建它们。

---

## 文档更新规则

行为变化时更新文档：

- 架构变化 -> `docs/01-architecture.md`
- 流程文件变化 -> `docs/02-flow-file-format.md`
- Runtime 变化 -> `docs/03-runtime-design.md`
- 新节点规范 -> `docs/04-node-development-guide.md`
- Adapter 变化 -> `docs/05-device-adapter-guide.md`
- Designer UI 变化 -> `docs/06-designer-ui-guide.md`
- 测试策略变化 -> `docs/07-test-plan.md`
- 集成方式变化 -> `docs/09-release-and-integration.md`

---

## 完成标准

任务完成前必须满足：

- 代码能编译。
- 相关测试通过，或者明确说明失败原因。
- 公共 API 足够清晰，便于上位机集成。
- 不引入禁止的项目引用。
- 不破坏 Demo。
- 新节点包含 Descriptor 和测试。

---

## Review 检查项

每次 review 检查：

- 依赖方向。
- UI / Runtime 分离。
- Node / Adapter 分离。
- 线程和取消逻辑。
- 序列化兼容性。
- 测试覆盖。
- 生产运行是否仍可无 WPF 执行。
## 2026-06 Current Implementation Notes

- Runtime supports output-port ordered fan-out scheduling through `RuntimeFlowPlan` and execution-path cycle detection.
- `FlowExecutionOptions` can enable parallel fan-out per output port; default remains sequential/shared-token.
- Common nodes include condition, AND join, motion, camera, queue-enabled recipe/save/database, frame group, scan group, stitching, and final fusion nodes.
- Camera callbacks are routed through disposable `ICameraFrameRouter`; `camera.image_callback` supports `StreamFrames` `Batch` and `PerFrame` modes.
- Save-like and heavy algorithm nodes can use bounded queues; `WaitForCompletion=false` returns after queue acceptance and relies on queue events for background status.
- `IVisionImage` includes `ImageKind`; fusion outputs include `HeightMap`, `TextureImage`, and `ConfidenceMap`.
- WPF Designer supports injected registries/devices/options, variable selection for input bindings and binding settings, and current stream/queue option values.
- Production WinForms hosts must continue to load `.flowruntime` and must not reference Designer UI assemblies.
