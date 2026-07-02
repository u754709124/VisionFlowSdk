# 00 - Project Brief

## 产品目标

构建一个独立的工业视觉流程 SDK：`VisionFlowSdk`�?

SDK 提供 UI 无关的流程运行时、Core 基础节点�?WPF 流程设计器。具体工程可以在此基础上注册项目专属节点，通过可视化流程编排：

- 设备控制
- 相机采集
- 光源控制
- 运控握手
- 算法配方调用
- 图像保存
- 数据库保�?
- 调试显示
- 流程发布

SDK 只内置基础流程能力；设备、算法、图像保存、数据库保存、拼图、扫描和融合节点由具体项目或项目专属节点库实现。WinForms 上位机生产运行时直接引用发布 DLL，加�?`.flowruntime` 并通过 `FlowRunner` 执行�?

## 核心原则

```text
WPF Designer = 编辑、配置、调试、发�?
Flow Runtime = 实际执行
WinForms Production App = 加载 .flowruntime 并执�?
```

生产运行时不能依�?WPF 流程图�?

## 技术目�?

- 目标框架�?NET Framework 4.8�?
- 生产宿主：WinForms�?
- 流程设计器：WPF�?
- 设计器可通过 ElementHost 宿主�?WinForms 中�?
- Runtime 不依�?WPF �?WinForms�?
- Core 基础节点不包含具体设�?SDK 逻辑�?
- 原有上位机代码通过 Adapter 复用�?

## 当前 SDK 内置范围

当前仓库保留�?

- Flow.Core
- Designer.Wpf
- Tests
- Demo.WinForms
- Demo.DesignerWpf

Core 内置基础节点�?

- Delay
- Log
- Variable Set
- Split
- AND Join
- Condition IF

不再内置�?

- 相机、光源、运控等设备节点
- 算法、图像保存、数据库保存节点
- 图像组汇合、拼图、扫描和融合节点

## 未来必须支持的工业场�?

以下场景由具体项目节点库�?SDK Core/Designer 之上实现，SDK 本身只提供运行时、发布、校验、序列化和基础流程编排能力�?

### 1. 单点单拍检�?

```text
运控到位 -> 光源 -> 相机拍照 -> 算法 -> 保存图片 -> 保存数据�?
```

### 2. 多点位图像组与拼�?

```text
点位1拍照 -> 点位2拍照 -> 图像组汇�?-> 拼图 -> 算法
```

### 3. 连续扫描采集

```text
运控到扫描起�?
  -> 相机外触发连续采�?
  -> 每帧预处�?
  -> �?N 张后扫描组汇�?
  -> 最�?3D 恢复 / 2D 融合
  -> 配方算法
```
