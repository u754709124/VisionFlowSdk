using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    // 节点执行策略属于发布协议，必须在生成运行态文件前完成数值和描述符契约校验。
    public sealed partial class FlowValidator
    {
        private static void ValidateNodeExecutionPolicies(
            IList<NodeDefinition> nodes,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (node == null)
                {
                    continue;
                }

                var field = "Nodes[" + index + "].ExecutionPolicy";
                var policy = node.ExecutionPolicy ?? new NodeExecutionPolicy();
                var retry = policy.RetryPolicy ?? new RetryPolicy();

                if (policy.TimeoutMs < 0)
                {
                    AddPolicyError(node, result, "TimeoutMs must not be negative.", field + ".TimeoutMs");
                }

                if (policy.MaxConcurrentExecutions <= 0)
                {
                    AddPolicyError(node, result, "MaxConcurrentExecutions must be greater than zero.", field + ".MaxConcurrentExecutions");
                }

                if (retry.MaxRetries < 0)
                {
                    AddPolicyError(node, result, "MaxRetries must not be negative.", field + ".RetryPolicy.MaxRetries");
                }

                if (retry.RetryIntervalMs < 0)
                {
                    AddPolicyError(node, result, "RetryIntervalMs must not be negative.", field + ".RetryPolicy.RetryIntervalMs");
                }

                if (!Enum.IsDefined(typeof(FailureStrategy), policy.FailureStrategy))
                {
                    AddPolicyError(node, result, "FailureStrategy is invalid.", field + ".FailureStrategy");
                    continue;
                }

                NodeDescriptor descriptor;
                if (descriptorsByNodeId == null || !descriptorsByNodeId.TryGetValue(node.Id ?? string.Empty, out descriptor) || descriptor == null)
                {
                    continue;
                }

                if (policy.FailureStrategy == FailureStrategy.ErrorBranch)
                {
                    ValidateErrorBranch(node, descriptor, field, result);
                }
                else if (policy.FailureStrategy == FailureStrategy.DefaultOutputs)
                {
                    ValidateDefaultOutputs(node, descriptor, policy.DefaultOutputs, field, result);
                }
            }
        }

        private static void ValidateErrorBranch(
            NodeDefinition node,
            NodeDescriptor descriptor,
            string field,
            FlowValidationResult result)
        {
            var ports = descriptor.OutputPorts;
            var hasErrorControlPort = false;
            if (ports != null)
            {
                for (var index = 0; index < ports.Count; index++)
                {
                    var port = ports[index];
                    if (port != null &&
                        string.Equals(port.Name, FlowPortNames.Error, StringComparison.OrdinalIgnoreCase) &&
                        port.Direction == FlowPortDirection.Output &&
                        port.DataType == FlowDataType.Control)
                    {
                        hasErrorControlPort = true;
                        break;
                    }
                }
            }

            if (!hasErrorControlPort)
            {
                result.AddError(
                    FlowValidationIssueCodes.NodeErrorPortMissing,
                    "ErrorBranch requires an Error output port with the Control data type.",
                    nodeId: node.Id,
                    field: field + ".FailureStrategy");
            }
        }

        private static void ValidateDefaultOutputs(
            NodeDefinition node,
            NodeDescriptor descriptor,
            IDictionary<string, object> configuredOutputs,
            string field,
            FlowValidationResult result)
        {
            var outputs = descriptor.Outputs ?? new List<NodeOutputDescriptor>();
            var defaults = configuredOutputs ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < outputs.Count; index++)
            {
                var output = outputs[index];
                if (output == null || string.IsNullOrWhiteSpace(output.Name))
                {
                    continue;
                }

                object value;
                if (!TryGetIgnoreCase(defaults, output.Name, out value))
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeDefaultOutputInvalid,
                        "DefaultOutputs must contain every declared node output: " + output.Name,
                        nodeId: node.Id,
                        field: field + ".DefaultOutputs." + output.Name);
                    continue;
                }

                if (value != null && !CanConvertTriggerDefault(value, output.DataType))
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeDefaultOutputInvalid,
                        "Default output cannot be converted to " + output.DataType + ": " + output.Name,
                        nodeId: node.Id,
                        field: field + ".DefaultOutputs." + output.Name);
                }
            }

            foreach (var item in defaults)
            {
                if (!ContainsOutput(outputs, item.Key))
                {
                    result.AddError(
                        FlowValidationIssueCodes.NodeDefaultOutputInvalid,
                        "DefaultOutputs contains an undeclared node output: " + item.Key,
                        nodeId: node.Id,
                        field: field + ".DefaultOutputs." + item.Key);
                }
            }
        }

        private static void AddPolicyError(
            NodeDefinition node,
            FlowValidationResult result,
            string message,
            string field)
        {
            result.AddError(
                FlowValidationIssueCodes.NodeExecutionPolicyInvalid,
                message,
                nodeId: node.Id,
                field: field);
        }
    }
}
