# 00 - Project Brief

## 产品目标

构建一个独立的工业视觉流程 SDK：`VisionFlowSdk`。

SDK 允许工程师通过可视化流程完成：

- 设备控制
- 相机采集
- 光源控制
- 运控握手
- 算法配方调用
- 图像保存
- 数据库保存
- 调试显示
- 流程发布

SDK 后续由 WinForms 上位机直接引用 DLL 使用。

## 核心原则

```text
WPF Designer = 编辑、配置、调试、发布
Flow Runtime = 实际执行
WinForms Production App = 加载 .flowruntime 并执行
```

生产运行时不能依赖 WPF 流程图。

## 技术目标

- 目标框架：.NET Framework 4.8。
- 生产宿主：WinForms。
- 流程设计器：WPF。
- 设计器可通过 ElementHost 宿主在 WinForms 中。
- Runtime 不依赖 WPF 或 WinForms。
- 节点不包含具体设备 SDK 逻辑。
- 原有上位机代码通过 Adapter 复用。

## MVP 范围

第一版至少实现：

- Flow.Core
- Flow.Nodes
- DeviceAdapters
- Designer.Wpf MVP
- Tests
- Demo.WinForms
- Demo.DesignerWpf

第一批节点：

- 相机参数设置
- 相机软触发
- 相机图像回调
- 光源控制
- 算法配方
- 图像保存
- 数据库保存
- Split
- AND Join
- Log
- Image Preview

## 未来必须支持的工业场景

### 1. 单点单拍检测

```text
运控到位 -> 光源 -> 相机拍照 -> 算法 -> 保存图片 -> 保存数据库
```

### 2. 多点位图像组与拼图

```text
点位1拍照 -> 点位2拍照 -> 图像组汇合 -> 拼图 -> 算法
```

### 3. 连续扫描采集

```text
运控到扫描起点
  -> 相机外触发连续采集
  -> 每帧预处理
  -> 满 N 张后扫描组汇合
  -> 最终 3D 恢复 / 2D 融合
  -> 配方算法
```
