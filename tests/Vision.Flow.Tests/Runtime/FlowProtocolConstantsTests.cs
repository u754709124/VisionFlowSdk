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

            AssertEx.Equal("NodeIdDuplicate", FlowValidationIssueCodes.NodeIdDuplicate, "validation code wire value");
            AssertEx.Equal("RuntimeContainsViewState", FlowValidationIssueCodes.RuntimeContainsViewState, "runtime view-state validation code wire value");
            AssertEx.Equal(".flowdesign", FlowFileExtensions.FlowDesign, "design file extension wire value");
            AssertEx.Equal(".flowruntime", FlowFileExtensions.FlowRuntime, "runtime file extension wire value");
            return Task.FromResult(0);
        }

        public static Task EnumWireValuesKeepExistingStrings()
        {
            AssertEx.Equal("Input", FlowEnumConverter.ToWireValue(FlowPortDirection.Input), "input port direction enum wire value");
            AssertEx.Equal("Output", FlowEnumConverter.ToWireValue(FlowPortDirection.Output), "output port direction enum wire value");
            AssertEx.Equal("Boolean", FlowEnumConverter.ToWireValue(FlowDataType.Boolean), "boolean data type enum wire value");
            AssertEx.Equal("Object", FlowEnumConverter.ToWireValue(FlowDataType.Object), "object data type enum wire value");
            AssertEx.Equal("Equal", FlowEnumConverter.ToWireValue(ConditionOperator.Equal), "condition operator enum wire value");
            AssertEx.Equal("Ignore", FlowEnumConverter.ToWireValue(FlowDuplicatePolicy.Ignore), "duplicate policy enum wire value");
            AssertEx.Equal("Warning", FlowEnumConverter.ToWireValue(FlowLogLevel.Warning), "log level enum wire value");
            AssertEx.Equal("TriggerId", FlowEnumConverter.ToWireValue(CameraFrameMatchMode.TriggerId), "camera match mode enum wire value");
            AssertEx.Equal("StreamFrames", FlowEnumConverter.ToWireValue(CameraCallbackMode.StreamFrames), "camera callback mode enum wire value");
            AssertEx.Equal("PerFrame", FlowEnumConverter.ToWireValue(CameraStreamOutputMode.PerFrame), "camera stream output mode enum wire value");
            AssertEx.Equal("Metadata", FlowEnumConverter.ToWireValue(FrameIndexSource.Metadata), "frame index source enum wire value");
            AssertEx.Equal("StopFlow", FlowEnumConverter.ToWireValue(FlowTaskQueueFullMode.StopFlow), "queue full mode enum wire value");

            FlowDuplicatePolicy duplicatePolicy;
            AssertEx.True(FlowEnumConverter.TryParse("replace", out duplicatePolicy), "Duplicate policy should parse case-insensitively.");
            AssertEx.Equal(FlowDuplicatePolicy.Replace, duplicatePolicy, "Parsed duplicate policy should match.");
            AssertEx.SequenceEqual(
                new[] { "Equal", "NotEqual", "GreaterThan", "LessThan", "Contains", "IsNull", "IsNotNull" },
                FlowEnumConverter.GetWireValues<ConditionOperator>(),
                "Condition operator wire value list should be stable.");
            return Task.FromResult(0);
        }
    }
}
