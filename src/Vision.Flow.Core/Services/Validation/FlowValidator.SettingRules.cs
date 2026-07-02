using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
        private static void ValidateRequiredSettings(
            IList<NodeDefinition> nodes,
            IDictionary<string, NodeDescriptor> descriptorsByNodeId,
            FlowValidationResult result)
        {
            for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
            {
                var node = nodes[nodeIndex];
                if (node == null || string.IsNullOrWhiteSpace(node.Id))
                {
                    continue;
                }

                NodeDescriptor descriptor;
                if (!descriptorsByNodeId.TryGetValue(node.Id, out descriptor) || descriptor.Settings == null)
                {
                    continue;
                }

                for (var settingIndex = 0; settingIndex < descriptor.Settings.Count; settingIndex++)
                {
                    var setting = descriptor.Settings[settingIndex];
                    if (setting == null || !setting.IsRequired || string.IsNullOrWhiteSpace(setting.Name))
                    {
                        continue;
                    }

                    if (!HasConfiguredValue(node, setting.Name))
                    {
                        result.AddError(
                            FlowValidationIssueCodes.RequiredSettingMissing,
                            "Required setting is missing or empty. Node=" + node.Id + ", Setting=" + setting.Name,
                            nodeId: node.Id,
                            field: "Nodes[" + nodeIndex + "].Settings." + setting.Name);
                    }
                }
            }
        }
    }
}
