using System.Collections.Generic;

namespace Vision.Flow.Core
{
    public sealed class NodeDescriptor
    {
        public NodeDescriptor()
        {
            InputPorts = new List<NodePortDescriptor>();
            OutputPorts = new List<NodePortDescriptor>();
            Settings = new List<NodeSettingDescriptor>();
            Outputs = new List<NodeOutputDescriptor>();
        }

        public string NodeType { get; set; }

        public string DisplayName { get; set; }

        public string Category { get; set; }

        public string Version { get; set; }

        public string Description { get; set; }

        public List<NodePortDescriptor> InputPorts { get; set; }

        public List<NodePortDescriptor> OutputPorts { get; set; }

        public List<NodeSettingDescriptor> Settings { get; set; }

        public List<NodeOutputDescriptor> Outputs { get; set; }
    }

    public sealed class NodePortDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Direction { get; set; }

        public string DataType { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }

    public sealed class NodeSettingDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string DataType { get; set; }

        public object DefaultValue { get; set; }

        public bool IsRequired { get; set; }

        public string Description { get; set; }
    }

    public sealed class NodeOutputDescriptor
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string DataType { get; set; }

        public string Description { get; set; }
    }
}
