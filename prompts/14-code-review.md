# 14 - Codex 架构和质量审查提示词

请对当前未提交改动做一次架构和质量审查。

重点检查：

1. 依赖方向：
   - Flow.Core 是否仍然无 UI、无 SDK 依赖。
   - Flow.Nodes 是否仍然只依赖 Flow.Core。
   - Designer.Wpf 是否没有承载生产运行逻辑。

2. 运行时/设计器分离：
   - 生产运行是否只依赖 `.flowruntime` 和 FlowRunner。
   - 是否有 WPF ViewModel 或 Control 泄漏到 Runtime。

3. 节点/适配器分离：
   - 节点是否只调用 Adapter 接口。
   - 是否误引入具体设备 SDK 或旧设备类。

4. 线程和异步：
   - 是否在相机回调中做了重活。
   - 是否缺少 CancellationToken。
   - 是否可能阻塞 UI 线程。

5. 序列化：
   - `.flowruntime` 是否不包含 view state。
   - 新字段是否可 JSON round-trip。

6. 测试：
   - 新增功能是否有测试。
   - 关键场景是否用 FakeAdapter 覆盖。

请输出：

- 发现的问题列表，按严重程度排序。
- 建议修改方案。
- 如果可以，请直接修复高置信度问题。
- 修复后运行 build/test.ps1。
