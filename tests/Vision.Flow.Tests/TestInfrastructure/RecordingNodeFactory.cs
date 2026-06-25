using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Tests
{
    // Recording nodes provide deterministic runtime probes for flow runner tests.
    internal sealed class RecordingNodeFactory : INodeFactory
    {
        public const string TypeName = "test.record";
        private readonly IList<string> _executionLog;

        public RecordingNodeFactory(IList<string> executionLog)
        {
            _executionLog = executionLog;
        }

        public string NodeType
        {
            get { return TypeName; }
        }

        public NodeDescriptor Descriptor
        {
            get
            {
                return new NodeDescriptor
                {
                    NodeType = TypeName,
                    DisplayName = "Recording Test Node",
                    Version = "1.0.0"
                };
            }
        }

        public IFlowNode Create(NodeDefinition definition)
        {
            return new RecordingNode(definition, _executionLog);
        }
    }
}
