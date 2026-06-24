# 07 - 光源、算法、保存、数据库节点

请实现阶段 7：光源、算法、图片保存、数据库节点。

请先阅读：

- AGENTS.md
- docs/04-node-development-guide.md
- docs/05-device-adapter-guide.md

在 Vision.Flow.Nodes 中实现：

1. LightControlNode
   NodeType: light.control

2. RecipeRunNode
   NodeType: recipe.run

3. ImageSaveNode
   NodeType: image.save

4. DatabaseSaveNode
   NodeType: database.save

要求：

## LightControlNode

Settings:

- LightId
- Channels
- StableDelayMs

通过 ILightAdapter 设置通道亮度。

## RecipeRunNode

Settings:

- RecipeId
- InputImageBinding
- TimeoutMs

通过 IRecipeAdapter 执行算法。

输出：

- Result
- ResultImage
- IsOk
- ElapsedMs

## ImageSaveNode

Settings:

- ImageBinding
- ResultImageBinding
- RootDirectory
- DirectoryTemplate
- FileNameTemplate

通过 IImageSaveAdapter 保存。

输出：

- ImagePath
- ResultImagePath

## DatabaseSaveNode

Settings:

- DatabaseId
- TableName
- FieldMappings

通过 IDatabaseAdapter 保存结果。

输出：

- Saved

测试要求：

1. 使用 FakeAdapters 跑通：
   camera soft trigger -> callback -> recipe -> image save -> database save
2. 验证变量绑定。
3. 验证保存路径输出。
4. 验证数据库保存调用。

验收条件：

1. build/test.ps1 通过。
2. 无 UI 依赖。
3. 无真实设备 SDK。
