using System.Threading.Tasks;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // 协议常量测试锁定 Core 内置节点和公共运行时字段，避免流程文件兼容值被误改。
    internal static class FlowProtocolConstantsTests
    {
        public static Task ConstantsKeepExistingWireValues()
        {
            AssertEx.Equal("delay.wait", FlowNodeTypes.DelayWait, "延时节点类型必须保持兼容。");
            AssertEx.Equal("log.write", FlowNodeTypes.LogWrite, "日志节点类型必须保持兼容。");
            AssertEx.Equal("variable.set", FlowNodeTypes.VariableSet, "变量节点类型必须保持兼容。");
            AssertEx.Equal("flow.split", FlowNodeTypes.FlowSplit, "分支节点类型必须保持兼容。");
            AssertEx.Equal("join.and", FlowNodeTypes.JoinAnd, "AND 汇合节点类型必须保持兼容。");
            AssertEx.Equal("condition.if", FlowNodeTypes.ConditionIf, "条件节点类型必须保持兼容。");
            AssertEx.Equal("In", FlowPortNames.In, "输入端口名必须保持兼容。");
            AssertEx.Equal("Next", FlowPortNames.Next, "默认成功端口名必须保持兼容。");
            AssertEx.Equal("Error", FlowPortNames.Error, "错误端口名必须保持兼容。");
            AssertEx.Equal("Input", FlowPortDirections.Input, "输入端口方向必须保持兼容。");
            AssertEx.Equal("Output", FlowPortDirections.Output, "输出端口方向必须保持兼容。");
            AssertEx.Equal("Control", FlowDataTypes.Control, "控制流数据类型必须保持兼容。");
            AssertEx.Equal("VariableName", FlowRuntimeDataKeys.VariableName, "运行事件变量名键必须保持兼容。");
            AssertEx.Equal("NodeIdDuplicate", FlowValidationIssueCodes.NodeIdDuplicate, "校验错误码必须保持兼容。");
            AssertEx.Equal(".flowruntime", FlowFileExtensions.FlowRuntime, "运行态文件扩展名必须保持兼容。");
            return Task.FromResult(0);
        }
    }
}
