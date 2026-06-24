# 13 - SDK DLL 打包脚本

请实现阶段 13：SDK DLL 打包脚本和输出说明。

目标：

后续 WinForms 上位机可以直接引用 SDK DLL。

请实现：

1. build/pack-sdk.ps1
2. 输出目录：
   artifacts/sdk
3. 拷贝以下 DLL：
   - Vision.Flow.Core.dll
   - Vision.Flow.Nodes.dll
   - Vision.DeviceAdapters.dll
   - Vision.Flow.Designer.Wpf.dll
4. 拷贝 XML 文档文件，如果项目启用了 XML documentation。
5. 拷贝 samples/flows 到 artifacts/samples/flows。
6. 生成 artifacts/sdk/README-INTEGRATION.md。

README-INTEGRATION.md 内容包括：

- 生产运行需要引用哪些 DLL。
- 如果需要 WPF 设计器，需要额外引用哪些 DLL。
- 如何注册 Adapter。
- 如何注册 CommonNodeRegistration。
- 如何加载 `.flowruntime`。
- 如何启动 FlowRunner。
- 如何订阅 RuntimeEvent。

验收条件：

1. build/pack-sdk.ps1 可以执行。
2. artifacts/sdk 结构清晰。
3. Demo.WinForms 能使用打包产物说明中的方式运行。
4. build/test.ps1 通过。
