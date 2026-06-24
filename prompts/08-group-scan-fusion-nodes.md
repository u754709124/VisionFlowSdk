# 08 - 图像组、拼图、扫描组和 3D/2D 合成节点

请实现阶段 8：图像组、拼图、扫描组和 3D/2D 合成节点。

请先阅读：

- docs/03-runtime-design.md
- docs/04-node-development-guide.md

新增节点：

1. FrameGroupJoinNode
   NodeType: group.frame_join

2. StitchNode
   NodeType: image.stitch

3. ScanGroupJoinNode
   NodeType: scan.group_join

4. FramePreprocessNode
   NodeType: frame.preprocess

5. Final3D2DFusionNode
   NodeType: fusion.final_3d_2d

第一版可以使用 Fake 算法逻辑，但接口和数据结构要设计清楚。

## FrameGroupJoinNode

- 按 CaptureGroupId 收集 Frame。
- 按 ShotIndex 汇合。
- ExpectedShotCount 可配置。
- 输出 GroupToken 或 FrameGroupResult。
- 支持重复 ShotIndex 检测。
- 支持超时或至少预留超时字段。

## StitchNode

- 输入 FrameGroup。
- 输出 StitchedImage。
- 第一版可以返回 FakeVisionImage。

## ScanGroupJoinNode

- 按 ScanGroupId 收集预处理结果。
- ExpectedFrameCount 可配置。
- 按 FrameIndex 排序。
- 输出 ScanGroupResult。

## FramePreprocessNode

- 输入 Frame/Image。
- 输出 FramePreprocessResult。
- 第一版可以返回 Fake preprocess result。

## Final3D2DFusionNode

- 输入 ScanGroupResult。
- 输出 Final3DImage 和 Final2DImage。
- 第一版可以返回 FakeVisionImage。

测试要求：

1. 两张图相同 CaptureGroupId，不同 ShotIndex，触发 FrameGroupJoin。
2. 重复 ShotIndex 被检测。
3. N 个 FramePreprocessResult 触发 ScanGroupJoin。
4. Final3D2DFusion 输出两个图像。
5. 测试结果顺序按 ShotIndex/FrameIndex 排序。

验收条件：

1. build/test.ps1 通过。
2. 数据结构适合后续真实算法替换。
3. 不引入真实图像算法 SDK。
