using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Validation
{
    public sealed partial class FlowValidator
    {
        private static void ValidateEntries(
            IList<FlowEntryDefinition> entries,
            IDictionary<string, NodeDefinition> nodeMap,
            FlowValidationResult result)
        {
            if (entries.Count == 0)
            {
                result.AddError(FlowValidationIssueCodes.EntriesMissing, "Runtime flow must contain at least one entry.", field: "Entries");
                return;
            }

            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    result.AddError(FlowValidationIssueCodes.EntryMissing, "Entry definition must not be null.", field: "Entries[" + index + "]");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.EntryName))
                {
                    result.AddError(FlowValidationIssueCodes.EntryNameMissing, "Entry name is required.", field: "Entries[" + index + "].EntryName");
                }
                else if (!entryNames.Add(entry.EntryName))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryNameDuplicate,
                        "Entry name must be unique: " + entry.EntryName,
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].EntryName");
                }

                if (string.IsNullOrWhiteSpace(entry.TargetNodeId))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryTargetMissing,
                        "Entry target node id is required.",
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].TargetNodeId");
                }
                else if (!nodeMap.ContainsKey(entry.TargetNodeId))
                {
                    result.AddError(
                        FlowValidationIssueCodes.EntryTargetMissing,
                        "Entry target node does not exist: " + entry.TargetNodeId,
                        entryName: entry.EntryName,
                        field: "Entries[" + index + "].TargetNodeId");
                }
            }
        }
    }
}
