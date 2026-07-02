using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        public static RuntimeFlowDefinition ToRuntimeFlowDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            var definition = new RuntimeFlowDefinition
            {
                FlowId = GetString(dictionary, "FlowId"),
                FlowName = GetString(dictionary, "FlowName"),
                SchemaVersion = GetInt32(dictionary, "SchemaVersion", 1),
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
                node.Settings = ToObjectDictionary(settingsValue);
            }

            object inputBindingsValue;
            if (TryGetValue(dictionary, "InputBindings", out inputBindingsValue))
            {
                node.InputBindings = ToBindingDictionary(inputBindingsValue);
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
            return new FlowEntryDefinition
            {
                EntryName = GetString(dictionary, "EntryName"),
                TargetNodeId = GetString(dictionary, "TargetNodeId")
            };
        }
    }
}
