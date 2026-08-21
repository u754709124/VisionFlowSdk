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

## 旧式 C# 项目的源文件清单

\`Vision.Flow.Core\` 和 \`Vision.Flow.Designer.Wpf\` 使用旧式 C# 项目格式。项目文件必须显式列出全部 \`.cs\` 源文件；不要仅依赖 \`Compile Include="**\\*.cs"\` 通配符。旧式项目系统可能继续使用过期的设计时源文件缓存，导致 IntelliSense 报告实际编译不存在的类型错误。

新增或删除这两个项目的源文件时，同步更新对应 \`.csproj\` 的 \`Compile\` 清单；如果 Visual Studio 仍显示已不存在的类型错误，关闭 Visual Studio 后删除解决方案或项目目录下的 \`.vs\` 缓存，再重新打开解决方案。
