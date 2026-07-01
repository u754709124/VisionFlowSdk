# QUICKSTART

## 1. 将本包解压到新仓库

```text
VisionFlowSdk/
```

## 2. 用 Codex 执行第一阶段

把 `prompts/00-master-start.md` 或 `prompts/01-solution-skeleton.md` 的内容作为首个任务发给 Codex。

## 3. 按阶段开发

每个阶段完成后运行：

```powershell
./build/build.ps1
./build/test.ps1
```

## 4. 关键约束

- Core 不依赖 UI。
- Core 内置节点只保留基础流程节点。
- 设备、算法、保存、拼图和融合节点放到具体项目或项目专属节点库。
- 真实设备逻辑通过 Core Adapter 契约或项目兼容契约接入。
- 生产运行加载 `.flowruntime`，不打开 WPF Designer。
