using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Tests
{
    internal static class SettingValueResolverTests
    {
        public static Task ResolvesConstantNodeOutputAndTokenPaths()
        {
            var flow = new RuntimeFlowDefinition { FlowId = "setting-resolver" };
            var node = new NodeDefinition { Id = "target", Type = "test.node" };
            node.Settings["Constant"] = NodeSettingValue.ForConstant(12);
            node.Settings["Output"] = NodeSettingValue.ForVariable(
                new VariableSelector
                {
                    Scope = VariableSelectorScope.NodeOutput,
                    Path = new List<string> { "source", "Payload", "Name" }
                },
                "fallback");
            node.Settings["Token"] = NodeSettingValue.ForVariable(VariableSelector.ForToken("Values", "BatchId"));

            var variables = new VariablePool();
            variables.Set("source.Payload", new Dictionary<string, object> { { "Name", "camera-1" } });
            var token = new FlowToken();
            token.Set("BatchId", "batch-7");
            var context = new FlowExecutionContext(flow, node, token, variables, new InMemoryFlowEventSink());

            AssertEx.Equal(12, context.GetSettingValue<int>("Constant"), "Constant setting should resolve its ConstantValue.");
            AssertEx.Equal("camera-1", context.GetSettingValue<string>("Output"), "NodeOutput selector should resolve nested output data.");
            AssertEx.Equal("batch-7", context.GetSettingValue<string>("Token"), "Token selector should resolve token values.");
            AssertEx.Equal("fallback", node.Settings["Output"].ConstantValue, "Variable mode should retain the prior constant value.");
            node.Settings["message"] = NodeSettingValue.ForVariable(VariableSelector.ForNodeOutput("SOURCE", "payload"));
            AssertEx.NotNull(context.GetSettingValue("Message"), "Setting names and node output variables should resolve case-insensitively.");
            return Task.FromResult(0);
        }

        public static Task DataTypeCompatibilityRules()
        {
            AssertEx.Equal(FlowDataTypeCompatibilityResult.Compatible, FlowDataTypeCompatibility.GetCompatibility(FlowDataType.Int32, FlowDataType.Int32), "Equal setting types should be compatible.");
            AssertEx.Equal(FlowDataTypeCompatibilityResult.Incompatible, FlowDataTypeCompatibility.GetCompatibility(FlowDataType.Int32, FlowDataType.Double), "Numeric widening should not bypass strict setting types.");
            AssertEx.Equal(FlowDataTypeCompatibilityResult.Incompatible, FlowDataTypeCompatibility.GetCompatibility(FlowDataType.Int64, FlowDataType.Double), "Potentially lossy numeric conversion should be incompatible.");
            AssertEx.Equal(FlowDataTypeCompatibilityResult.Incompatible, FlowDataTypeCompatibility.GetCompatibility(FlowDataType.Object, FlowDataType.String), "Object should not bind to a concrete setting type.");
            AssertEx.Equal(FlowDataTypeCompatibilityResult.Incompatible, FlowDataTypeCompatibility.GetCompatibility(FlowDataType.String, FlowDataType.Object), "Concrete values should not bind to Object settings.");
            AssertEx.Equal(FlowDataTypeCompatibilityResult.Incompatible, FlowDataTypeCompatibility.GetCompatibility(FlowDataType.Control, FlowDataType.Object), "Control is never a setting value.");
            AssertEx.Equal(FlowDataTypeCompatibilityResult.Incompatible, FlowDataTypeCompatibility.GetCompatibility(FlowDataType.IVisionImage, FlowDataType.String), "Vision images only bind to the same type or Object.");
            AssertEx.True(
                FlowDataTypeCompatibility.IsCompatible(
                    FlowDataType.Object,
                    null,
                    typeof(TypedPayload),
                    FlowDataType.Object,
                    null,
                    typeof(ITypedPayload)),
                "A typed Object source should bind to an assignable Object contract.");
            AssertEx.False(
                FlowDataTypeCompatibility.IsCompatible(
                    FlowDataType.Object,
                    null,
                    null,
                    FlowDataType.Object,
                    null,
                    typeof(ITypedPayload)),
                "An untyped Object source must not bypass a typed Object target.");
            AssertEx.True(
                FlowDataTypeCompatibility.IsCompatible(
                    FlowDataType.Object,
                    null,
                    typeof(TypedPayload),
                    FlowDataType.Object,
                    null,
                    null),
                "A generic Object target should continue accepting typed Object sources.");
            return Task.FromResult(0);
        }

        private interface ITypedPayload
        {
        }

        private sealed class TypedPayload : ITypedPayload
        {
        }
    }
}
