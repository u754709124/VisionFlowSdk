# 01 - 创建解决方案骨架

请实现阶段 1：创建 VisionFlowSdk 解决方案骨架。

请严格遵守 AGENTS.md 的依赖规则。

需要创建：

1. 解决方案：
   VisionFlowSdk.sln

2. 类库项目：
   src/Vision.Flow.Core
   src/Vision.Flow.Nodes
   src/Vision.DeviceAdapters

3. WPF 项目：
   src/Vision.Flow.Designer.Wpf

4. 测试项目：
   tests/Vision.Flow.Tests

5. Demo 项目：
   demos/Vision.Flow.Demo.WinForms
   demos/Vision.Flow.Demo.DesignerWpf

6. build 脚本：
   build/build.ps1
   build/test.ps1
   build/clean.ps1

7. 基础文件：
   README.md
   CHANGELOG.md
   .editorconfig
   .gitignore

项目引用关系：

- Vision.Flow.Nodes -> Vision.Flow.Core
- Vision.DeviceAdapters -> Vision.Flow.Core
- Vision.Flow.Designer.Wpf -> Vision.Flow.Core
- Vision.Flow.Designer.Wpf -> Vision.Flow.Nodes
- Vision.Flow.Tests -> Vision.Flow.Core
- Vision.Flow.Tests -> Vision.Flow.Nodes
- Vision.Flow.Tests -> Vision.DeviceAdapters
- Vision.Flow.Demo.WinForms -> Vision.Flow.Core
- Vision.Flow.Demo.WinForms -> Vision.Flow.Nodes
- Vision.Flow.Demo.WinForms -> Vision.DeviceAdapters
- Vision.Flow.Demo.DesignerWpf -> Vision.Flow.Core
- Vision.Flow.Demo.DesignerWpf -> Vision.Flow.Nodes
- Vision.Flow.Demo.DesignerWpf -> Vision.DeviceAdapters
- Vision.Flow.Demo.DesignerWpf -> Vision.Flow.Designer.Wpf

不要实现业务逻辑。

只创建骨架、基础类、占位测试、能编译的 Demo 空窗口。

验收条件：

1. build/build.ps1 成功。
2. build/test.ps1 成功。
3. Core 和 Nodes 没有 UI 引用。
4. 输出项目结构摘要。
