using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        public static object ToSerializableRuntimeFlowDefinition(RuntimeFlowDefinition definition)
        {
            var result = new Dictionary<string, object>
            {
                { "FlowId", definition.FlowId },
                { "FlowName", definition.FlowName },
                { "SchemaVersion", definition.SchemaVersion },
                { "Version", definition.Version },
                { "Settings", NormalizeObject(definition.Settings) },
                { "Nodes", ToSerializableNodes(definition.Nodes) },
                { "Edges", ToSerializableEdges(definition.Edges) },
                { "Entries", ToSerializableEntries(definition.Entries) }
            };

            return result;
        }

        public static object ToSerializableDesignDocument(FlowDesignDocument document)
        {
            return new Dictionary<string, object>
            {
                { "FlowId", document.FlowId },
                { "FlowName", document.FlowName },
                { "SchemaVersion", document.SchemaVersion },
                { "Runtime", document.Runtime == null ? null : ToSerializableRuntimeFlowDefinition(document.Runtime) },
                { "View", document.View == null ? null : ToSerializableView(document.View) }
            };
        }

        private static object ToSerializableNodes(IEnumerable<NodeDefinition> nodes)
        {
            var result = new List<object>();
            if (nodes == null)
            {
                return result;
            }

            foreach (var node in nodes)
            {
                if (node == null)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(new Dictionary<string, object>
                {
                    { "Id", node.Id },
                    { "Type", node.Type },
                    { "Name", node.Name },
                    { "Version", node.Version },
                    { "Settings", ToSerializableSettings(node.Settings) }
                });
            }

            return result;
        }

        private static object ToSerializableEdges(IEnumerable<EdgeDefinition> edges)
        {
            var result = new List<object>();
            if (edges == null)
            {
                return result;
            }

            foreach (var edge in edges)
            {
                if (edge == null)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(new Dictionary<string, object>
                {
                    { "FromNodeId", edge.FromNodeId },
                    { "FromPort", edge.FromPort },
                    { "ToNodeId", edge.ToNodeId },
                    { "ToPort", edge.ToPort }
                });
            }

            return result;
        }

        private static object ToSerializableEntries(IEnumerable<FlowEntryDefinition> entries)
        {
            var result = new List<object>();
            if (entries == null)
            {
                return result;
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(new Dictionary<string, object>
                {
                    { "EntryName", entry.EntryName },
                    { "TargetNodeId", entry.TargetNodeId },
                    { "SourceNodeId", entry.SourceNodeId },
                    { "TriggerKind", FlowEnumConverter.ToWireValue(entry.TriggerKind) },
                    { "Inputs", ToSerializableTriggerInputs(entry.Inputs) },
                    { "ExecutionPolicy", ToSerializableTriggerExecutionPolicy(entry.ExecutionPolicy) }
                });
            }

            return result;
        }

        private static object ToSerializableTriggerInputs(IEnumerable<TriggerInputDescriptor> inputs)
        {
            var result = new List<object>();
            if (inputs == null)
            {
                return result;
            }

            foreach (var input in inputs)
            {
                result.Add(input == null
                    ? null
                    : new Dictionary<string, object>
                    {
                        { "Name", input.Name },
                        { "DisplayName", input.DisplayName },
                        { "DataType", FlowEnumConverter.ToWireValue(input.DataType) },
                        { "IsRequired", input.IsRequired },
                        { "DefaultValue", NormalizeObject(input.DefaultValue) },
                        { "Description", input.Description }
                    });
            }

            return result;
        }

        private static object ToSerializableTriggerExecutionPolicy(TriggerExecutionPolicy policy)
        {
            var effective = policy ?? new TriggerExecutionPolicy();
            return new Dictionary<string, object>
            {
                { "MaxConcurrentRuns", effective.MaxConcurrentRuns },
                { "QueueCapacity", effective.QueueCapacity },
                { "QueueFullBehavior", FlowEnumConverter.ToWireValue(effective.QueueFullBehavior) }
            };
        }

        private static object ToSerializableView(FlowViewState view)
        {
            return new Dictionary<string, object>
            {
                { "Zoom", view.Zoom },
                { "OffsetX", view.OffsetX },
                { "OffsetY", view.OffsetY },
                { "CanvasWidth", view.CanvasWidth },
                { "CanvasHeight", view.CanvasHeight },
                { "Nodes", ToSerializableViewNodes(view.Nodes) }
            };
        }

        private static object ToSerializableViewNodes(IDictionary<string, NodeViewState> nodes)
        {
            var result = new Dictionary<string, object>();
            if (nodes == null)
            {
                return result;
            }

            foreach (var item in nodes)
            {
                var state = item.Value;
                result[item.Key] = state == null
                    ? null
                    : new Dictionary<string, object>
                    {
                        { "X", state.X },
                        { "Y", state.Y },
                        { "IsCollapsed", state.IsCollapsed }
                    };
            }

            return result;
        }
    }
}
