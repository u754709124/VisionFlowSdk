# 05 - Device Adapter Guide

## 目的

Adapter 契约让项目专属节点可以调用现有上位机设备、算法、保存和数据库逻辑，同时避免 Core 直接引用具体 SDK。

## 当前边界

Core 保留 Adapter 接口和数据契约，例如：

- `ICameraAdapter`
- `IVisionImage`

SDK 不再内置 Fake Adapter 项目，也不再内置设备节点。Fake 设备和真实设备适配器应由具体项目、Demo 或测试项目自行提供。

光源、运动、Recipe、图像保存、数据库保存和队列服务不再作为 Core 公共契约发布；项目如需这些能力，应在项目专属节点库或上位机应用中定义自己的接口。

## 规则

- 节点不直接调用真实 SDK。
- 节点通过 Adapter 接口访问设备或上位机服务。
- Adapter 负责包装真实 SDK、旧服务或测试桩。
- 相机回调线程只做轻量封装，不执行重算法。
- 图像对象应通过 `IVisionImage` 或项目自有兼容实现流转。
- 长耗时 Adapter 操作必须支持 `CancellationToken` 和超时策略。

## 图像生命周期

当图像跨异步任务、队列或延迟保存边界时，应使用 `IVisionImage.CloneReference()` 或项目自有引用计数机制保持底层句柄有效。拥有原生句柄的一方负责释放。
