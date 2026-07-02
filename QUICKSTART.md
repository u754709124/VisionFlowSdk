# QUICKSTART

## 1. 将本包解压到新仓�?

```text
VisionFlowSdk/
```

## 2. �?Codex 执行第一阶段

�?`prompts/00-master-start.md` �?`prompts/01-solution-skeleton.md` 的内容作为首个任务发�?Codex�?

## 3. 按阶段开�?

每个阶段完成后运行：

```powershell
./build/build.ps1
./build/test.ps1
```

## 4. 引用命名空间

运行时宿主通常按职责引�?Core API�?

```csharp
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Nodes;
```

设计器宿主额外引用：

```csharp
using Vision.Flow.Designer.Wpf.Controls;
```

## 5. 关键约束

- Core 不依�?UI�?
- Core 内置节点只保留基础流程节点�?
- 设备、算法、保存、拼图和融合节点放到具体项目或项目专属节点库�?
- 真实设备逻辑通过 Core Adapter 契约或项目兼容契约接入�?
- 生产运行加载 `.flowruntime`，不打开 WPF Designer�?
