using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    // 节点专项规则仅覆盖 Core 内置基础节点；设备和算法节点由具体项目注册并自行约束。
    public sealed partial class FlowValidator
    {
        private static void ValidateNodeSpecificRules(
            IList<NodeDefinition> nodes,
            IList<EdgeDefinition> edges,
            IList<FlowEntryDefinition> entries,
            IDictionary<string, EnvironmentVariableDefinition> environmentVariables,
            IDictionary<string, GlobalVariableDefinition> globalVariables,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || string.IsNullOrWhiteSpace(node.Type))
                {
                    continue;
                }

                var fieldPrefix = "Nodes[" + index + "].Settings.";
                if (string.Equals(node.Type, FlowNodeTypes.DelayWait, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateNonNegativeInt(node, FlowSettingNames.DelayMs, 0, fieldPrefix, result);
                }

                if (string.Equals(node.Type, FlowNodeTypes.JoinAnd, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateAndJoinNode(node, fieldPrefix, result);
                }

                if (string.Equals(node.Type, FlowNodeTypes.ConditionIf, StringComparison.OrdinalIgnoreCase))
                {
                    ValidateConditionNode(
                        node,
                        fieldPrefix,
                        edges,
                        entries,
                        environmentVariables,
                        globalVariables,
                        descriptorsByNodeId,
                        result);
                }
            }
        }

        private static void ValidateAndJoinNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            ValidatePositiveInt(node, FlowSettingNames.ExpectedInputCount, 2, fieldPrefix, result);
            ValidateNonNegativeInt(node, FlowSettingNames.TimeoutMs, 0, fieldPrefix, result);
            ValidateDuplicatePolicy(node, fieldPrefix, result);
        }

        private static void ValidateConditionNode(
            NodeDefinition node,
            string fieldPrefix,
            IList<EdgeDefinition> edges,
            IList<FlowEntryDefinition> entries,
            IDictionary<string, EnvironmentVariableDefinition> environmentVariables,
            IDictionary<string, GlobalVariableDefinition> globalVariables,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            object operatorName;
            if (!TryGetConstantSettingValue(node, FlowSettingNames.Operator, out operatorName))
            {
                operatorName = ConditionOperator.Equal;
            }

            ConditionOperator parsedOperator;
            if (!FlowEnumConverter.TryParse(operatorName, out parsedOperator))
            {
                result.AddError(
                    FlowValidationIssueCodes.SettingValueInvalid,
                    "Operator must be Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThanOrEqual, or GreaterThan.",
                    nodeId: node.Id,
                    field: fieldPrefix + FlowSettingNames.Operator);
                return;
            }

            NodeSettingValue leftSetting;
            if (node.Settings == null ||
                !node.Settings.TryGetValue(FlowSettingNames.LeftValue, out leftSetting) ||
                leftSetting == null ||
                leftSetting.Mode != NodeSettingValueMode.Variable ||
                leftSetting.Selector == null)
            {
                return;
            }

            ConditionValueType leftType;
            if (!TryResolveConditionValueType(
                node,
                leftSetting.Selector,
                edges,
                entries,
                environmentVariables,
                globalVariables,
                descriptorsByNodeId,
                out leftType))
            {
                return;
            }

            if (!leftType.IsSupported)
            {
                result.AddError(
                    FlowValidationIssueCodes.VariableTypeIncompatible,
                    "条件左值只支持数值、字符串或枚举类型。",
                    nodeId: node.Id,
                    field: fieldPrefix + FlowSettingNames.LeftValue);
                return;
            }

            if (!leftType.IsNumeric && !IsEqualityOperator(parsedOperator))
            {
                result.AddError(
                    FlowValidationIssueCodes.SettingValueInvalid,
                    "字符串和枚举条件只支持 Equal 或 NotEqual。",
                    nodeId: node.Id,
                    field: fieldPrefix + FlowSettingNames.Operator);
            }

            NodeSettingValue rightSetting;
            if (node.Settings == null ||
                !node.Settings.TryGetValue(FlowSettingNames.RightValue, out rightSetting) ||
                rightSetting == null)
            {
                return;
            }

            if (rightSetting.Mode == NodeSettingValueMode.Variable &&
                rightSetting.Selector != null)
            {
                ConditionValueType rightType;
                if (TryResolveConditionValueType(
                    node,
                    rightSetting.Selector,
                    edges,
                    entries,
                    environmentVariables,
                    globalVariables,
                    descriptorsByNodeId,
                    out rightType) &&
                    !AreConditionTypesCompatible(leftType, rightType))
                {
                    result.AddError(
                        FlowValidationIssueCodes.VariableTypeIncompatible,
                        "条件左右变量类型不兼容。数值可跨数值类型比较；枚举必须属于同一个枚举类。",
                        nodeId: node.Id,
                        field: fieldPrefix + FlowSettingNames.RightValue);
                }
                return;
            }

            if (rightSetting.Mode == NodeSettingValueMode.Constant &&
                !IsConditionConstantCompatible(leftType, rightSetting.ConstantValue))
            {
                result.AddError(
                    FlowValidationIssueCodes.SettingValueInvalid,
                    leftType.EnumType == null
                        ? "条件右侧固定值与左值类型不兼容。"
                        : "条件右侧固定值必须是枚举 " + leftType.EnumType.Name + " 的有效成员。",
                    nodeId: node.Id,
                    field: fieldPrefix + FlowSettingNames.RightValue);
            }
        }

        private static bool IsEqualityOperator(ConditionOperator operatorName)
        {
            return operatorName == ConditionOperator.Equal ||
                operatorName == ConditionOperator.NotEqual;
        }

        private static bool AreConditionTypesCompatible(
            ConditionValueType left,
            ConditionValueType right)
        {
            if (left == null || right == null)
                return false;
            if (left.IsNumeric || right.IsNumeric)
                return left.IsNumeric && right.IsNumeric;
            if (left.DataType != FlowDataType.String ||
                right.DataType != FlowDataType.String)
            {
                return false;
            }
            if (left.EnumType != null || right.EnumType != null)
                return left.EnumType != null && left.EnumType == right.EnumType;
            return true;
        }

        private static bool IsConditionConstantCompatible(
            ConditionValueType left,
            object value)
        {
            if (left == null || value == null)
                return false;
            if (left.IsNumeric)
            {
                decimal converted;
                return IsNumericValue(value) &&
                    decimal.TryParse(
                        Convert.ToString(value, CultureInfo.InvariantCulture),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out converted);
            }
            string text = value as string;
            if (text == null)
                return false;
            return left.EnumType == null || Enum.GetNames(left.EnumType).Any(
                name => string.Equals(name, text, StringComparison.Ordinal));
        }

        private static bool IsNumericValue(object value)
        {
            if (value == null)
                return false;
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryResolveConditionValueType(
            NodeDefinition targetNode,
            VariableSelector selector,
            IList<EdgeDefinition> edges,
            IList<FlowEntryDefinition> entries,
            IDictionary<string, EnvironmentVariableDefinition> environmentVariables,
            IDictionary<string, GlobalVariableDefinition> globalVariables,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            out ConditionValueType valueType)
        {
            valueType = null;
            if (selector == null || selector.Path == null || selector.Path.Count == 0)
                return false;

            if (selector.Scope == VariableSelectorScope.NodeOutput)
            {
                if (selector.Path.Count < 2)
                    return false;
                NodeDescriptor descriptor;
                if (!descriptorsByNodeId.TryGetValue(selector.Path[0], out descriptor))
                    return false;
                NodeOutputDescriptor output = descriptor.Outputs.FirstOrDefault(x =>
                    x != null && string.Equals(
                        x.Name,
                        selector.Path[1],
                        StringComparison.OrdinalIgnoreCase));
                if (output == null)
                    return false;
                if (selector.Path.Count == 2)
                {
                    valueType = new ConditionValueType(
                        output.DataType,
                        output.EnumType,
                        output.ObjectType);
                    return true;
                }
                Type memberType;
                if (selector.Path.Count != 3 ||
                    output.DataType != FlowDataType.Object ||
                    output.ObjectType == null ||
                    !TryGetFirstLayerMemberType(
                        output.ObjectType,
                        selector.Path[2],
                        out memberType))
                {
                    return false;
                }
                FlowTypeMetadata metadata = FlowTypeMetadata.FromClrType(memberType);
                valueType = new ConditionValueType(
                    metadata.DataType,
                    metadata.EnumType,
                    metadata.ObjectType);
                return true;
            }

            if (selector.Scope == VariableSelectorScope.EnvironmentVariable)
            {
                EnvironmentVariableDefinition definition;
                if (selector.Path.Count != 1 ||
                    environmentVariables == null ||
                    !environmentVariables.TryGetValue(selector.Path[0], out definition))
                {
                    return false;
                }
                valueType = new ConditionValueType(definition.DataType, null, null);
                return true;
            }

            if (selector.Scope == VariableSelectorScope.GlobalVariable)
            {
                GlobalVariableDefinition definition;
                if (selector.Path.Count != 1 ||
                    globalVariables == null ||
                    !globalVariables.TryGetValue(selector.Path[0], out definition))
                {
                    return false;
                }
                valueType = new ConditionValueType(definition.DataType, null, null);
                return true;
            }

            if (selector.Scope == VariableSelectorScope.TriggerInput)
            {
                var types = (entries ?? new List<FlowEntryDefinition>())
                    .Where(entry => entry != null &&
                        CanEntryReachNode(entry, targetNode.Id, edges))
                    .SelectMany(entry => entry.Inputs ?? new List<TriggerInputDescriptor>())
                    .Where(input => input != null && string.Equals(
                        input.Name,
                        selector.Path[0],
                        StringComparison.OrdinalIgnoreCase))
                    .Select(input => input.DataType)
                    .Distinct()
                    .ToList();
                if (selector.Path.Count != 1 || types.Count != 1)
                    return false;
                valueType = new ConditionValueType(types[0], null, null);
                return true;
            }

            if (selector.Scope == VariableSelectorScope.Token &&
                selector.Path.Count == 1)
            {
                if (string.Equals(selector.Path[0], "TokenId", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(selector.Path[0], "CaptureFrameId", StringComparison.OrdinalIgnoreCase))
                {
                    valueType = new ConditionValueType(FlowDataType.String, null, null);
                    return true;
                }
                if (string.Equals(selector.Path[0], "CreatedAtUtc", StringComparison.OrdinalIgnoreCase))
                {
                    valueType = new ConditionValueType(FlowDataType.DateTime, null, null);
                    return true;
                }
            }
            return false;
        }

        private sealed class ConditionValueType
        {
            /// <summary>
            /// 创建条件操作数的设计期类型快照。
            /// </summary>
            public ConditionValueType(
                FlowDataType dataType,
                Type enumType,
                Type objectType)
            {
                DataType = dataType;
                EnumType = enumType;
                ObjectType = objectType;
            }

            /// <summary>获取流程数据类型。</summary>
            public FlowDataType DataType { get; private set; }

            /// <summary>获取枚举 CLR 类型；非枚举值为 null。</summary>
            public Type EnumType { get; private set; }

            /// <summary>获取对象 CLR 类型；非对象值为 null。</summary>
            public Type ObjectType { get; private set; }

            /// <summary>获取是否属于 IF 支持的数值类型族。</summary>
            public bool IsNumeric
            {
                get
                {
                    return DataType == FlowDataType.Int32 ||
                        DataType == FlowDataType.Int64 ||
                        DataType == FlowDataType.Double;
                }
            }

            /// <summary>获取是否属于 IF 支持的数值、字符串或枚举类型。</summary>
            public bool IsSupported
            {
                get
                {
                    return IsNumeric ||
                        (DataType == FlowDataType.String && ObjectType == null);
                }
            }
        }

        private static void ValidateDuplicatePolicy(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            object duplicatePolicy;
            if (!TryGetConstantSettingValue(node, FlowSettingNames.DuplicatePolicy, out duplicatePolicy))
            {
                duplicatePolicy = FlowDuplicatePolicy.Error;
            }

            FlowDuplicatePolicy parsedPolicy;
            if (!FlowEnumConverter.TryParse(duplicatePolicy, out parsedPolicy))
            {
                result.AddError(FlowValidationIssueCodes.DuplicatePolicyInvalid, "DuplicatePolicy must be Error, Ignore, or Replace.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.DuplicatePolicy);
            }
        }
    }
}

