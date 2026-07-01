using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    /// <summary>
    /// 延时节点配置，保存执行前等待的毫秒数。
    /// </summary>
    public sealed class DelayNodeConfig
    {
        public DelayNodeConfig()
        {
            DelayMs = 0;
        }

        public int DelayMs { get; set; }
    }

    public sealed class DelayNodeFactory : BaseNodeFactory<DelayNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.DelayWait;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return DelayNodeDescriptor.Create(); }
        }

        protected override DelayNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new DelayNodeConfig
            {
                DelayMs = GetInt32Setting(definition, FlowSettingNames.DelayMs, 0)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, DelayNodeConfig config)
        {
            return new DelayNode(config);
        }
    }

    public sealed class DelayNode : IFlowNode
    {
        private readonly DelayNodeConfig _config;

        public DelayNode(DelayNodeConfig config)
        {
            _config = config ?? new DelayNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var delayMs = ResolveDelayMs(context);
            if (delayMs < 0)
            {
                return NodeExecutionResult.Failure("DelayMs must be greater than or equal to zero.");
            }

            if (delayMs > 0)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            return NodeExecutionResult.Success(
                FlowPortNames.Next,
                new Dictionary<string, object>
                {
                    { FlowOutputNames.DelayMs, delayMs }
                });
        }

        private int ResolveDelayMs(FlowExecutionContext context)
        {
            var value = context.GetInputValue(FlowSettingNames.DelayMs);
            if (value == null)
            {
                return _config.DelayMs;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    public static class DelayNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = DelayNodeFactory.TypeName,
                DisplayName = "Delay",
                Category = "Common",
                Version = "1.0.0",
                Description = "Waits for a configured duration before continuing.",
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
                        Description = "Continues after the delay."
                    },
                    new NodePortDescriptor
                    {
                        Name = FlowPortNames.Error,
                        DisplayName = FlowPortNames.Error,
                        Direction = FlowPortDirections.Output,
                        DataType = FlowDataTypes.Control,
                        Description = "Routes invalid delay configuration."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = FlowOutputNames.DelayMs,
                        DisplayName = "Delay (ms)",
                        DataType = FlowDataTypes.Int32,
                        DefaultValue = 0,
                        IsRequired = true,
                        Description = "Delay duration in milliseconds."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = FlowSettingNames.DelayMs,
                        DisplayName = "Delay (ms)",
                        DataType = FlowDataTypes.Int32,
                        Description = "The resolved delay duration."
                    }
                }
            };
        }
    }
}
