# 18 - 生成示例流程提示词

请生成或更新 samples/flows 下的示例流程。

需要三个示例：

## 1. single-shot.flowdesign 和 single-shot.flowruntime

流程：

```text
ManualStart
  -> camera.set_parameters
  -> camera.soft_trigger
  -> camera.image_callback
  -> recipe.run
  -> image.save
  -> database.save
```

## 2. two-position-stitch.flowdesign

流程：

```text
Position1Frame
  -> group.frame_join

Position2Frame
  -> group.frame_join

group.frame_join
  -> image.stitch
  -> recipe.run
  -> image.save
  -> database.save
```

## 3. continuous-scan.flowdesign

流程：

```text
CameraFrameStream
  -> frame.preprocess
  -> scan.group_join
  -> fusion.final_3d_2d
  -> recipe.run
  -> image.save
  -> database.save
```

要求：

1. 文件格式符合 docs/02-flow-file-format.md。
2. `.flowruntime` 不包含 view state。
3. `.flowdesign` 包含合理节点坐标。
4. Demo.WinForms 默认加载 single-shot.flowruntime。
5. 增加测试验证 sample flow 可以反序列化并通过基本校验。
