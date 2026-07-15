using System;
using System.Collections.Generic;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Validation
{
    // 校验器负责检查运行态流程的结构、节点契约和变量绑定边界。
    public sealed partial class FlowValidator
    {
        private readonly NodeRegistry _nodeRegistry;

        public FlowValidator(NodeRegistry nodeRegistry)
        {
            if (nodeRegistry == null)
            {
                throw new ArgumentNullException("nodeRegistry");
            }

            _nodeRegistry = nodeRegistry;
        }

        public FlowValidationResult Validate(FlowDesignDocument document)
        {
            var result = new FlowValidationResult();
            if (document == null)
            {
                result.AddError(FlowValidationIssueCodes.FlowDesignMissing, "Flow design document is required.");
                return result;
            }

            if (document.Runtime == null)
            {
                result.AddError(FlowValidationIssueCodes.RuntimeMissing, "Flow design document must contain a runtime definition.");
                return result;
            }

            return Validate(document.Runtime);
        }

        public FlowValidationResult Validate(RuntimeFlowDefinition definition)
        {
            var result = new FlowValidationResult();
            if (definition == null)
            {
                result.AddError(FlowValidationIssueCodes.RuntimeMissing, "Runtime flow definition is required.");
                return result;
            }

            if (definition.SchemaVersion != FlowSchema.CurrentVersion)
            {
                result.AddError(FlowValidationIssueCodes.SchemaVersionUnsupported, "SchemaVersion must be " + FlowSchema.CurrentVersion + ".", field: "SchemaVersion");
            }

            if (string.IsNullOrWhiteSpace(definition.FlowId))
            {
                result.AddError(FlowValidationIssueCodes.FlowIdMissing, "FlowId is required.", field: "FlowId");
            }

            var nodes = definition.Nodes ?? new List<NodeDefinition>();
            var nodeMap = BuildNodeMap(nodes, result);
            var descriptorsByNodeId = ValidateNodeFactories(nodes, result);

            if (nodes.Count == 0)
            {
                result.AddError(FlowValidationIssueCodes.NodesMissing, "Runtime flow must contain at least one node.", field: "Nodes");
            }

            ValidateEdges(definition.Edges ?? new List<EdgeDefinition>(), nodeMap, descriptorsByNodeId, result);
            ValidateEntries(definition.Entries ?? new List<FlowEntryDefinition>(), nodeMap, result);
            ValidateRequiredSettings(nodes, descriptorsByNodeId, result);
            ValidateNodeExecutionPolicies(nodes, descriptorsByNodeId, result);
            ValidateSettingValues(
                nodes,
                definition.Edges ?? new List<EdgeDefinition>(),
                definition.Entries ?? new List<FlowEntryDefinition>(),
                nodeMap,
                descriptorsByNodeId,
                result);
            ValidateNoDesignerState(definition, result);
            ValidateNodeSpecificRules(nodes, result);

            return result;
        }
    }
}
