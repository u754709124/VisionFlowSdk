using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    // �ڵ�ר���������� Core ���û����ڵ㣻�豸���㷨�ڵ��ɾ�����Ŀע�Ტ����Լ����
    public sealed partial class FlowValidator
    {
        private static readonly string[] ConditionOperators = new[]
        {
            "Equal",
            "NotEqual",
            "GreaterThan",
            "LessThan",
            "Contains",
            "IsNull",
            "IsNotNull"
        };

        private static void ValidateNodeSpecificRules(
            IList<NodeDefinition> nodes,
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
                    ValidateConditionNode(node, fieldPrefix, result);
                }
            }
        }

        private static void ValidateAndJoinNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            ValidatePositiveInt(node, FlowSettingNames.ExpectedInputCount, 2, fieldPrefix, result);
            ValidateNonNegativeInt(node, FlowSettingNames.TimeoutMs, 0, fieldPrefix, result);
            ValidateDuplicatePolicy(node, fieldPrefix, result);
        }

        private static void ValidateConditionNode(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            var operatorName = GetSettingString(node, FlowSettingNames.Operator, "Equal");
            if (!IsOneOf(operatorName, ConditionOperators))
            {
                result.AddError(FlowValidationIssueCodes.SettingValueInvalid, "Operator must be Equal, NotEqual, GreaterThan, LessThan, Contains, IsNull, or IsNotNull.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.Operator);
            }
        }

        private static void ValidateDuplicatePolicy(NodeDefinition node, string fieldPrefix, FlowValidationResult result)
        {
            var duplicatePolicy = GetSettingString(node, FlowSettingNames.DuplicatePolicy, FlowDuplicatePolicies.Error);
            if (!IsOneOf(duplicatePolicy, FlowDuplicatePolicies.Error, FlowDuplicatePolicies.Ignore, FlowDuplicatePolicies.Replace))
            {
                result.AddError(FlowValidationIssueCodes.DuplicatePolicyInvalid, "DuplicatePolicy must be Error, Ignore, or Replace.", nodeId: node.Id, field: fieldPrefix + FlowSettingNames.DuplicatePolicy);
            }
        }
    }
}

