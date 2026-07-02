using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Nodes
{
    /// <summary>
    /// 执行流分支节点配置，目前不需要额外参数。
    /// </summary>
    public sealed class SplitNodeConfig
    {
    }

    public sealed class SplitNodeFactory : BaseNodeFactory<SplitNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.FlowSplit;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return SplitNodeDescriptor.Create(); }
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, SplitNodeConfig config)
        {
            return new SplitNode(config);
        }
    }

    public sealed class SplitNode : IFlowNode
    {
        public SplitNode(SplitNodeConfig config)
        {
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(NodeExecutionResult.Success(FlowPortNames.Next));
        }
    }

    public static class SplitNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = SplitNodeFactory.TypeName,
                DisplayName = "Split",
                Category = "Common",
                Version = "1.0.0",
                Description = "Routes execution through the default branch.",
                InputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.In,
                        DisplayName = FlowPortNames.In,
                        Direction = FlowPortDirections.Input,
                        DataType = FlowDataTypes.Control,
                        IsRequired = true,
                        Description = "Execution input."
                    }
                },
                OutputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.Next,
                        DisplayName = FlowPortNames.Next,
                        Direction = FlowPortDirections.Output,
                        DataType = FlowDataTypes.Control,
                        Description = "Default output branch."
                    }
                }
            };
        }
    }
}
