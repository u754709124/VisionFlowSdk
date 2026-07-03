# 07 - Test Plan

## 测试项目

```text
tests/Vision.Flow.Tests
```

## 覆盖范围

- Runtime：线性执行、扇出、并行扇出、重入汇合、环检测、缺失入口、错误路由、超时路由、事件顺序、Continuation 调度、监听节点启动/停止，以及已移除公共面的守卫。
- Serialization / Publish：`.flowdesign` round-trip、`.flowruntime` round-trip、发布后移除 view state、样例流程校验。
- Core 节点：注册、日志事件、延时、变量写入、AND Join、Condition 分支、相机软触发、相机硬触发回调、相机参数设置。
- Core 契约：`VisionImageReference` 生命周期、`ICameraAdapter.GrabOneAsync` 接入、硬触发回调不在相机回调线程执行后续节点。
- Designer：属性面板只读、节点库交互、拖拽、停止调试、按钮状态恢复、节点卡片运行状态显示。
- Demo：解决方案构建覆盖 WinForms Demo 和 Designer WPF Demo。

## 不再覆盖

SDK 测试不再覆盖 light/motion/recipe/save/database/group/scan/stitch/fusion 节点，也不再覆盖已移除的相机帧路由实现。具体项目实现这些能力时，应在项目自己的测试集中覆盖。

## 命令

```powershell
./build/build.ps1
./build/test.ps1
```
