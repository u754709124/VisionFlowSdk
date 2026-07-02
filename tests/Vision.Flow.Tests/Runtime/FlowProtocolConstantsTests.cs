using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;

namespace Vision.Flow.Tests
{
    // 协议常量测试保护节点、端口、事件和文件扩展名等 wire value，避免重构目录时误改生产文件协议。
    internal static class FlowProtocolConstantsTests
    {
        public static Task ConstantsKeepExistingWireValues()
        {
            AssertEx.Equal("delay.wait", FlowNodeTypes.DelayWait, "delay node type wire value");
            AssertEx.Equal("log.write", FlowNodeTypes.LogWrite, "log node type wire value");
            AssertEx.Equal("variable.set", FlowNodeTypes.VariableSet, "variable node type wire value");
            AssertEx.Equal("flow.split", FlowNodeTypes.FlowSplit, "split node type wire value");
            AssertEx.Equal("join.and", FlowNodeTypes.JoinAnd, "join node type wire value");
            AssertEx.Equal("condition.if", FlowNodeTypes.ConditionIf, "condition node type wire value");

            AssertEx.Equal("In", FlowPortNames.In, "input control port wire value");
            AssertEx.Equal("Next", FlowPortNames.Next, "default success port wire value");
            AssertEx.Equal("Error", FlowPortNames.Error, "error port wire value");
            AssertEx.Equal("True", FlowPortNames.True, "condition true port wire value");
            AssertEx.Equal("False", FlowPortNames.False, "condition false port wire value");
            AssertEx.Equal("Input", FlowPortDirections.Input, "input port direction wire value");
            AssertEx.Equal("Output", FlowPortDirections.Output, "output port direction wire value");

            AssertEx.Equal("Control", FlowDataTypes.Control, "control data type wire value");
            AssertEx.Equal("String", FlowDataTypes.String, "string data type wire value");
            AssertEx.Equal("Result", FlowOutputNames.Result, "result output wire value");
            AssertEx.Equal("Value", FlowOutputNames.Value, "value output wire value");
            AssertEx.Equal("ElapsedMs", FlowOutputNames.ElapsedMs, "elapsed output wire value");
            AssertEx.Equal("DelayMs", FlowSettingNames.DelayMs, "delay setting wire value");
            AssertEx.Equal("VariableName", FlowSettingNames.VariableName, "variable setting wire value");
            AssertEx.Equal("LeftBinding", FlowSettingNames.LeftBinding, "condition left binding wire value");

            AssertEx.Equal("VariableName", FlowRuntimeDataKeys.VariableName, "runtime event variable data key");
            AssertEx.Equal("QueueName", FlowRuntimeDataKeys.QueueName, "runtime queue data key");
            AssertEx.Equal("ElapsedMs", FlowRuntimeDataKeys.ElapsedMs, "runtime elapsed data key");
            AssertEx.Equal("default", FlowQueueNames.Default, "default queue name wire value");
            AssertEx.Equal("StopFlow", FlowQueueFullModeNames.StopFlow, "queue full mode wire value");

            AssertEx.Equal("WaitNextFrame", CameraCallbackModes.WaitNextFrame, "camera callback mode wire value");
            AssertEx.Equal("StreamFrames", CameraCallbackModes.StreamFrames, "camera callback mode wire value");
            AssertEx.Equal("Batch", CameraStreamOutputModes.Batch, "camera stream output mode wire value");
            AssertEx.Equal("Increment", FrameIndexSources.Increment, "frame index source wire value");

            AssertEx.Equal("NodeIdDuplicate", FlowValidationIssueCodes.NodeIdDuplicate, "validation code wire value");
            AssertEx.Equal("RuntimeContainsViewState", FlowValidationIssueCodes.RuntimeContainsViewState, "runtime view-state validation code wire value");
            AssertEx.Equal(".flowdesign", FlowFileExtensions.FlowDesign, "design file extension wire value");
            AssertEx.Equal(".flowruntime", FlowFileExtensions.FlowRuntime, "runtime file extension wire value");
            return Task.FromResult(0);
        }
    }
}
