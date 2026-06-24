# 07 - Test Plan

## 测试项目

```text
tests/Vision.Flow.Tests
```

## Runtime 测试

- FlowRunner 执行线性流程。
- FlowRunner 发布 NodeStarted / NodeCompleted。
- 节点失败发布 NodeFailed。
- TriggerAsync 路由到正确入口。
- VariablePool 存取变量。
- CancellationToken 被尊重。

## Serialization 测试

- `.flowdesign` round-trip。
- `.flowruntime` round-trip。
- 发布后移除 ViewState。
- 必要字段被保留。

## Node 测试

- CameraSetParameterNode。
- CameraSoftTriggerNode。
- CameraImageCallbackNode。
- LightControlNode。
- RecipeRunNode。
- ImageSaveNode。
- DatabaseSaveNode。
- AndJoinNode。
- FrameGroupJoinNode。
- ScanGroupJoinNode。

## Integration 测试

### 1. 单拍流程

```text
camera.set_parameters -> camera.soft_trigger -> camera.image_callback -> recipe.run -> image.save -> database.save
```

### 2. 双点位图像组流程

```text
frame1 -> frame2 -> group.frame_join -> image.stitch -> recipe.run
```

### 3. 连续扫描流程

```text
N frames -> frame.preprocess -> scan.group_join -> fusion.final_3d_2d -> recipe.run
```

## Demo 测试

- Demo.WinForms 不打开 WPF Designer 也能运行 `.flowruntime`。
- Demo.DesignerWpf 能打开 `.flowdesign` 并发布 `.flowruntime`。

## 完成标准

- Build 通过。
- 相关测试通过。
- 依赖规则未破坏。
- 生产运行仍保持 UI 无关。
