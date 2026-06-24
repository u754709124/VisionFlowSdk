# 12 - 流程发布与校验

请实现阶段 12：流程校验和发布服务。

请先阅读：

- docs/02-flow-file-format.md
- docs/03-runtime-design.md

在 Vision.Flow.Core 中实现：

- FlowValidator
- FlowValidationResult
- FlowValidationIssue
- FlowPublishService

校验规则：

1. FlowId 不能为空。
2. NodeId 不能重复。
3. Node type 必须存在于 NodeRegistry。
4. Edge 的 from/to node 必须存在。
5. Edge 的端口必须在 NodeDescriptor 中存在。
6. 必填 setting 不能为空。
7. Entry target node 必须存在。
8. `.flowruntime` 不能包含 view state。
9. 变量引用的 nodeId 和 output name 必须存在。
10. CameraId/LightId/RecipeId 等设备 ID 校验先预留接口，第一版可选。

发布规则：

1. 输入 FlowDesignDocument。
2. 输出 RuntimeFlowDefinition。
3. 移除所有 view state。
4. 保留 nodes、edges、entries、settings、inputBindings。
5. 返回校验结果。

测试要求：

1. 重复 NodeId 返回错误。
2. 悬空 Edge 返回错误。
3. 缺少 required setting 返回错误。
4. 发布后 runtime 不包含 view。
5. 合法流程发布成功。

验收条件：

1. build/test.ps1 通过。
2. WPF Designer 发布按钮调用 FlowPublishService。
