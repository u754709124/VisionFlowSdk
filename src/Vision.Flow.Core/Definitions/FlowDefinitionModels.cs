using System.Collections.Generic;

namespace Vision.Flow.Core
{
    public sealed class FlowDesignDocument
    {
        public FlowDesignDocument()
        {
            SchemaVersion = 1;
            Runtime = new RuntimeFlowDefinition();
            View = new FlowViewState();
        }

        public string FlowId { get; set; }

        public string FlowName { get; set; }

        public int SchemaVersion { get; set; }

        public RuntimeFlowDefinition Runtime { get; set; }

        public FlowViewState View { get; set; }
    }

    public sealed class FlowViewState
    {
        public FlowViewState()
        {
            Zoom = 1.0;
            Nodes = new Dictionary<string, NodeViewState>();
        }

        public double Zoom { get; set; }

        public double OffsetX { get; set; }

        public double OffsetY { get; set; }

        public Dictionary<string, NodeViewState> Nodes { get; set; }
    }

    public sealed class NodeViewState
    {
        public double X { get; set; }

        public double Y { get; set; }

        public bool IsCollapsed { get; set; }
    }

    public sealed class RuntimeFlowDefinition
    {
        public RuntimeFlowDefinition()
        {
            SchemaVersion = 1;
            Nodes = new List<NodeDefinition>();
            Edges = new List<EdgeDefinition>();
            Entries = new List<FlowEntryDefinition>();
            Settings = new Dictionary<string, object>();
        }

        public string FlowId { get; set; }

        public string FlowName { get; set; }

        public int SchemaVersion { get; set; }

        public string Version { get; set; }

        public List<NodeDefinition> Nodes { get; set; }

        public List<EdgeDefinition> Edges { get; set; }

        public List<FlowEntryDefinition> Entries { get; set; }

        public Dictionary<string, object> Settings { get; set; }
    }

    public sealed class NodeDefinition
    {
        public NodeDefinition()
        {
            Settings = new Dictionary<string, object>();
            InputBindings = new Dictionary<string, VariableBinding>();
        }

        public string Id { get; set; }

        public string Type { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        public Dictionary<string, object> Settings { get; set; }

        public Dictionary<string, VariableBinding> InputBindings { get; set; }
    }

    public sealed class EdgeDefinition
    {
        public string FromNodeId { get; set; }

        public string FromPort { get; set; }

        public string ToNodeId { get; set; }

        public string ToPort { get; set; }
    }

    public sealed class FlowEntryDefinition
    {
        public string EntryName { get; set; }

        public string TargetNodeId { get; set; }
    }
}
