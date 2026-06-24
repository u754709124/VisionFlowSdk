# 09 - WinForms 生产 Demo

请实现阶段 9：Vision.Flow.Demo.WinForms。

目标：

证明生产上位机不打开 WPF 流程图，也能加载 `.flowruntime` 并运行。

请实现一个简单 WinForms UI：

顶部按钮：

- Load Runtime Flow
- Start Runner
- Stop Runner
- Trigger Manual Start
- Trigger Motion Arrived

左侧：

- Flow info
- Registered fake devices

中间：

- Runtime event log ListView/DataGridView

右侧：

- Last token data
- Last node output summary
- Last image summary

要求：

1. 注册 FakeCameraAdapter、FakeLightAdapter、FakeRecipeAdapter、FakeImageSaveAdapter、FakeDatabaseAdapter。
2. 注册 CommonNodeRegistration。
3. 加载 samples/flows/single-shot.flowruntime。
4. 点击 Trigger Manual Start 后执行流程。
5. 显示 RuntimeEvent。
6. 显示相机输出图像的宽高、FrameId、GrabTime。
7. 不引用或打开 FlowDesignerControl。
8. 如果 Demo.WinForms 当前引用 Designer.Wpf，请移除，除非有单独的编辑按钮且默认生产路径不使用它。

验收条件：

1. Demo.WinForms 可启动。
2. 可以运行 single-shot.flowruntime。
3. 运行日志显示节点执行顺序。
4. build/test.ps1 通过。
