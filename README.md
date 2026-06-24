# VisionFlowSdk

`VisionFlowSdk` 是一个独立的工业视觉流程 SDK，用于将工业视觉软件中的设备控制、图像采集、算法处理、图像保存、数据库保存、调试显示等能力编排成可发布、可运行的流程。

## 核心目标

WPF 流程图只用于：

- 流程编排
- 节点参数配置
- 调试运行
- 发布运行态流程

生产运行时由 WinForms 上位机加载 `.flowruntime` 文件，通过 `FlowRunner` 直接执行，不打开 WPF 流程图。

```text
设计态：
.flowdesign -> WPF Designer -> 校验/调试/发布

生产态：
.flowruntime -> FlowRunner -> Common Nodes -> Device Adapters -> 原有设备/算法/存储逻辑
```

## 解决方案结构

```text
VisionFlowSdk
│
├─ src
│  ├─ Vision.Flow.Core
│  ├─ Vision.Flow.Nodes
│  ├─ Vision.DeviceAdapters
│  └─ Vision.Flow.Designer.Wpf
│
├─ tests
│  └─ Vision.Flow.Tests
│
├─ demos
│  ├─ Vision.Flow.Demo.WinForms
│  └─ Vision.Flow.Demo.DesignerWpf
│
├─ docs
│
├─ prompts
│
├─ samples
│  └─ flows
│
└─ build
```

## 项目职责

| 项目 | 职责 |
|---|---|
| `Vision.Flow.Core` | 流程定义、执行引擎、Token、变量池、运行事件、流程发布/加载、Adapter 接口 |
| `Vision.Flow.Nodes` | 公共节点运行逻辑，不包含 UI，不包含真实设备 SDK |
| `Vision.DeviceAdapters` | 默认注册表、Fake 设备、Demo 适配器、适配器基类 |
| `Vision.Flow.Designer.Wpf` | WPF 流程设计器、节点画布、属性面板、变量选择器、调试显示 |
| `Vision.Flow.Tests` | 单元测试、节点测试、序列化测试、集成测试 |
| `Vision.Flow.Demo.WinForms` | 模拟生产上位机，无流程图运行 |
| `Vision.Flow.Demo.DesignerWpf` | 独立设计器 Demo |

## 首个 MVP

第一版建议先跑通：

```text
ManualStart
  -> camera.set_parameters
  -> camera.soft_trigger
  -> camera.image_callback
  -> recipe.run
  -> image.save
  -> database.save
```

## 构建

```powershell
./build/build.ps1
```

## 测试

```powershell
./build/test.ps1
```

## 打包 SDK

```powershell
./build/pack-sdk.ps1
```

## 文档

请从以下文档开始阅读：

```text
docs/00-project-brief.md
docs/01-architecture.md
docs/02-flow-file-format.md
docs/03-runtime-design.md
docs/04-node-development-guide.md
docs/05-device-adapter-guide.md
docs/06-designer-ui-guide.md
docs/07-test-plan.md
docs/08-demo-guide.md
docs/09-release-and-integration.md
docs/10-codex-workflow.md
```
