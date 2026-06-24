# 10 - Codex Workflow

## 工作方式

使用小步任务。

不要一次让 Codex 实现完整 SDK。

推荐流程：

1. 让 Codex 阅读文档并给出简短计划。
2. 让 Codex 实现一个模块。
3. 要求增加测试。
4. 要求运行 build/test 脚本。
5. 要求总结改动和遗留问题。

## 提示词应包含

- 范围
- 要修改的项目
- 架构约束
- 验收标准
- 要运行的测试
- 明确不要做什么

## 编码前 Codex 应阅读

- `AGENTS.md`
- `README.md`
- 相关 `docs/*.md`

## 编码后 Codex 应报告

- 修改内容
- 新增或更新的测试
- 运行过的命令
- 失败原因
- 后续建议

## 常用命令

```powershell
./build/build.ps1
./build/test.ps1
```

## Review 检查

- 依赖方向
- UI/Runtime 分离
- Node/Adapter 分离
- 线程风险
- 序列化兼容性
- 测试覆盖
