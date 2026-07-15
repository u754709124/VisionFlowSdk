using System;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        public static RuntimeFlowDefinition ToRuntimeFlowDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            var schemaVersion = GetInt32(dictionary, "SchemaVersion", 0);
            FlowSchema.EnsureSupported(schemaVersion);
            var definition = new RuntimeFlowDefinition
            {
                FlowId = GetString(dictionary, "FlowId"),
                FlowName = GetString(dictionary, "FlowName"),
                SchemaVersion = schemaVersion,
                Version = GetString(dictionary, "Version")
            };

            object settingsValue;
            if (TryGetValue(dictionary, "Settings", out settingsValue))
            {
                definition.Settings = ToObjectDictionary(settingsValue);
            }

            object nodesValue;
            if (TryGetValue(dictionary, "Nodes", out nodesValue))
            {
                foreach (var node in AsEnumerable(nodesValue))
                {
                    definition.Nodes.Add(ToNodeDefinition(node));
                }
            }

            object edgesValue;
            if (TryGetValue(dictionary, "Edges", out edgesValue))
            {
                foreach (var edge in AsEnumerable(edgesValue))
                {
                    definition.Edges.Add(ToEdgeDefinition(edge));
                }
            }

            object entriesValue;
            if (TryGetValue(dictionary, "Entries", out entriesValue))
            {
                foreach (var entry in AsEnumerable(entriesValue))
                {
                    definition.Entries.Add(ToEntryDefinition(entry));
                }
            }

            return definition;
        }
        private static NodeDefinition ToNodeDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            var node = new NodeDefinition
            {
                Id = GetString(dictionary, "Id"),
                Type = GetString(dictionary, "Type"),
                Name = GetString(dictionary, "Name"),
                Version = GetString(dictionary, "Version")
            };

            object settingsValue;
            if (TryGetValue(dictionary, "Settings", out settingsValue))
            {
                node.Settings = ToSettingDictionary(settingsValue);
            }

            return node;
        }

        private static EdgeDefinition ToEdgeDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            return new EdgeDefinition
            {
                FromNodeId = GetString(dictionary, "FromNodeId"),
                FromPort = GetString(dictionary, "FromPort"),
                ToNodeId = GetString(dictionary, "ToNodeId"),
                ToPort = GetString(dictionary, "ToPort")
            };
        }

        private static FlowEntryDefinition ToEntryDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            var entry = new FlowEntryDefinition
            {
                EntryName = GetString(dictionary, "EntryName"),
                TargetNodeId = GetString(dictionary, "TargetNodeId"),
                SourceNodeId = GetString(dictionary, "SourceNodeId")
            };

            object triggerKindValue;
            if (TryGetValue(dictionary, "TriggerKind", out triggerKindValue) && triggerKindValue != null)
            {
                try
                {
                    entry.TriggerKind = FlowEnumConverter.Parse<FlowTriggerKind>(triggerKindValue);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException("Entry TriggerKind is invalid.", ex);
                }
            }

            object inputsValue;
            if (TryGetValue(dictionary, "Inputs", out inputsValue))
            {
                foreach (var inputValue in AsEnumerable(inputsValue))
                {
                    entry.Inputs.Add(ToTriggerInputDescriptor(inputValue));
                }
            }

            object policyValue;
            if (TryGetValue(dictionary, "ExecutionPolicy", out policyValue) && policyValue != null)
            {
                entry.ExecutionPolicy = ToTriggerExecutionPolicy(policyValue);
            }

            return entry;
        }

        private static TriggerInputDescriptor ToTriggerInputDescriptor(object value)
        {
            var dictionary = AsDictionary(value);
            var dataTypeText = GetString(dictionary, "DataType");
            FlowDataType dataType;
            try
            {
                dataType = FlowEnumConverter.Parse<FlowDataType>(dataTypeText);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException("Trigger input DataType is invalid.", ex);
            }

            return new TriggerInputDescriptor
            {
                Name = GetString(dictionary, "Name"),
                DisplayName = GetString(dictionary, "DisplayName"),
                DataType = dataType,
                IsRequired = GetBoolean(dictionary, "IsRequired", false),
                DefaultValue = GetObject(dictionary, "DefaultValue"),
                Description = GetString(dictionary, "Description")
            };
        }

        private static TriggerExecutionPolicy ToTriggerExecutionPolicy(object value)
        {
            var dictionary = AsDictionary(value);
            var policy = new TriggerExecutionPolicy
            {
                MaxConcurrentRuns = GetInt32(dictionary, "MaxConcurrentRuns", 1),
                QueueCapacity = GetInt32(dictionary, "QueueCapacity", 64)
            };

            object behaviorValue;
            if (TryGetValue(dictionary, "QueueFullBehavior", out behaviorValue) && behaviorValue != null)
            {
                try
                {
                    policy.QueueFullBehavior = FlowEnumConverter.Parse<TriggerQueueFullBehavior>(behaviorValue);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException("Trigger execution QueueFullBehavior is invalid.", ex);
                }
            }

            return policy;
        }
    }
}
