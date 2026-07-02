using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;

namespace Vision.Flow.Tests
{
    // 鍗忚甯搁噺娴嬭瘯淇濇姢鑺傜偣銆佺鍙ｃ€佷簨浠跺拰鏂囦欢鎵╁睍鍚嶇瓑 wire value锛岄伩鍏嶉噸鏋勭洰褰曟椂璇敼鐢熶骇鏂囦欢鍗忚銆?
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
            AssertEx.Equal("ElapsedMs", FlowRuntimeDataKeys.ElapsedMs, "runtime elapsed data key");

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
