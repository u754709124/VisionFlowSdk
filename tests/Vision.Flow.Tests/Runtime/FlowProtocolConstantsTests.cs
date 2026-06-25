using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Tests
{
    // 协议常量测试锁定公开字符串值，避免治理硬编码时改变序列化和集成契约。
    internal static class FlowProtocolConstantsTests
    {
        public static Task ConstantsKeepExistingWireValues()
        {
            AssertEx.Equal("camera.image_callback", FlowNodeTypes.CameraImageCallback, "相机回调节点类型必须保持兼容。");
            AssertEx.Equal("recipe.run", FlowNodeTypes.RecipeRun, "配方节点类型必须保持兼容。");
            AssertEx.Equal("group.frame_join", FlowNodeTypes.GroupFrameJoin, "图像组汇合节点类型必须保持兼容。");
            AssertEx.Equal("In", FlowPortNames.In, "输入端口名必须保持兼容。");
            AssertEx.Equal("Next", FlowPortNames.Next, "默认成功端口名必须保持兼容。");
            AssertEx.Equal("Error", FlowPortNames.Error, "错误端口名必须保持兼容。");
            AssertEx.Equal("Input", FlowPortDirections.Input, "输入端口方向必须保持兼容。");
            AssertEx.Equal("Output", FlowPortDirections.Output, "输出端口方向必须保持兼容。");
            AssertEx.Equal("Control", FlowDataTypes.Control, "控制流数据类型必须保持兼容。");
            AssertEx.Equal("CameraId", FlowSettingNames.CameraId, "相机设置键必须保持兼容。");
            AssertEx.Equal("Image", FlowOutputNames.Image, "图像输出名必须保持兼容。");
            AssertEx.Equal("FrameId", FlowOutputNames.FrameId, "帧号输出名必须保持兼容。");
            AssertEx.Equal("ScanGroupId", FlowMetadataKeys.ScanGroupId, "扫描组元数据键必须保持兼容。");
            AssertEx.Equal("VariableName", FlowRuntimeDataKeys.VariableName, "运行事件变量名键必须保持兼容。");
            AssertEx.Equal("NodeIdDuplicate", FlowValidationIssueCodes.NodeIdDuplicate, "校验错误码必须保持兼容。");
            AssertEx.Equal(".flowruntime", FlowFileExtensions.FlowRuntime, "运行态文件扩展名必须保持兼容。");
            return Task.FromResult(0);
        }
    }
}
