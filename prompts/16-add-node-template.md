# 16 - 新增节点提示词模板

请新增一个公共节点：

节点名称：

[例如 LightControlNode]

节点类型：

[例如 light.control]

节点用途：

[描述节点业务]

节点输入：

[描述输入端口和变量绑定]

节点输出：

[描述输出端口和输出变量]

节点配置：

[列出 settings]

执行逻辑：

[描述运行逻辑]

要求：

1. 放在 Vision.Flow.Nodes。
2. 不引用 WPF、WinForms、具体设备 SDK。
3. 只通过 Vision.Flow.Core 中的接口访问外部能力。
4. 包含 Config、Node、Factory、Descriptor。
5. 注册到 CommonNodeRegistration。
6. 增加单元测试或集成测试。
7. 更新 docs/04-node-development-guide.md 或补充节点列表。
8. 如果影响 Demo，更新 sample flow。

验收条件：

1. build/test.ps1 通过。
2. 节点 descriptor 可供 WPF 属性面板使用。
3. 节点执行失败时返回明确错误或发布 NodeFailed。
