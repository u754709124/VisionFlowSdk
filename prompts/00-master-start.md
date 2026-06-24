# 00 - Codex 总启动提示词

你现在在一个新的仓库中开发独立工业视觉流程 SDK：VisionFlowSdk。

请先阅读并遵守：

- AGENTS.md
- README.md
- docs/00-project-brief.md
- docs/01-architecture.md
- docs/10-codex-workflow.md

目标：

创建一个可被后续 WinForms 上位机直接引用 DLL 的独立解决方案。

技术要求：

- 目标框架：.NET Framework 4.8。
- WPF 仅用于流程设计器。
- 生产运行时不能依赖 WPF 流程图。
- 流程执行引擎必须被 WinForms 和 WPF 共用。
- 节点不能包含具体设备 SDK 逻辑，只能通过 Adapter 接口访问设备。
- 真实设备适配由上位机项目实现，本 SDK 只提供接口、Fake Adapter、公共节点和设计器。

请先不要一次性实现所有功能。

第一步只创建解决方案骨架、项目引用关系、README、基础 build/test 脚本，并确保可以编译。

解决方案结构：

```text
src/Vision.Flow.Core
src/Vision.Flow.Nodes
src/Vision.DeviceAdapters
src/Vision.Flow.Designer.Wpf
tests/Vision.Flow.Tests
demos/Vision.Flow.Demo.WinForms
demos/Vision.Flow.Demo.DesignerWpf
```

依赖规则：

- Vision.Flow.Core 不允许引用 WPF、WinForms、具体设备 SDK。
- Vision.Flow.Nodes 只允许引用 Vision.Flow.Core。
- Vision.DeviceAdapters 只允许引用 Vision.Flow.Core。
- Vision.Flow.Designer.Wpf 可以引用 Vision.Flow.Core 和 Vision.Flow.Nodes。
- Demo 项目可以引用 Core、Nodes、DeviceAdapters，Designer Demo 可以引用 Designer.Wpf。

验收条件：

1. 解决方案可以打开。
2. 所有项目可以编译。
3. 项目引用关系符合 AGENTS.md。
4. build/build.ps1 可以编译解决方案。
5. tests/Vision.Flow.Tests 存在并可运行，即使目前只有一个占位测试。
6. 不引入真实设备 SDK。

完成后请说明你创建了哪些文件、项目引用关系、运行了哪些命令。
