using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Nodes;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // ��¼�ڵ�Ϊ FlowRunner �����ṩȷ���Ե�����̽�롣
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
