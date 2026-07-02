using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        public static FlowDesignDocument ToDesignDocument(object value)
        {
            var dictionary = AsDictionary(value);
            var document = new FlowDesignDocument
            {
                FlowId = GetString(dictionary, "FlowId"),
                FlowName = GetString(dictionary, "FlowName"),
                SchemaVersion = GetInt32(dictionary, "SchemaVersion", 1)
            };

            object runtimeValue;
            if (TryGetValue(dictionary, "Runtime", out runtimeValue))
            {
                document.Runtime = ToRuntimeFlowDefinition(runtimeValue);
            }

            object viewValue;
            if (TryGetValue(dictionary, "View", out viewValue))
            {
                document.View = ToFlowViewState(viewValue);
            }

            return document;
        }
        private static FlowViewState ToFlowViewState(object value)
        {
            var dictionary = AsDictionary(value);
            var view = new FlowViewState
            {
                Zoom = GetDouble(dictionary, "Zoom", 1.0),
                OffsetX = GetDouble(dictionary, "OffsetX", 0),
                OffsetY = GetDouble(dictionary, "OffsetY", 0),
                CanvasWidth = GetDouble(dictionary, "CanvasWidth", FlowViewState.DefaultCanvasWidth),
                CanvasHeight = GetDouble(dictionary, "CanvasHeight", FlowViewState.DefaultCanvasHeight)
            };

            object nodesValue;
            if (TryGetValue(dictionary, "Nodes", out nodesValue))
            {
                foreach (var item in ToObjectDictionary(nodesValue))
                {
                    view.Nodes[item.Key] = ToNodeViewState(item.Value);
                }
            }

            return view;
        }

        private static NodeViewState ToNodeViewState(object value)
        {
            var dictionary = AsDictionary(value);
            return new NodeViewState
            {
                X = GetDouble(dictionary, "X", 0),
                Y = GetDouble(dictionary, "Y", 0),
                IsCollapsed = GetBoolean(dictionary, "IsCollapsed", false)
            };
        }
    }
}
