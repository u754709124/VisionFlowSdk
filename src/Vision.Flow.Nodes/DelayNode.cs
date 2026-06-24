using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
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
        public const string TypeName = "delay.wait";

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
                DelayMs = GetInt32Setting(definition, "DelayMs", 0)
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
                "Next",
                new Dictionary<string, object>
                {
                    { "DelayMs", delayMs }
                });
        }

        private int ResolveDelayMs(FlowExecutionContext context)
        {
            var value = context.GetInputValue("DelayMs");
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
                        Name = "In",
                        DisplayName = "In",
                        Direction = "Input",
                        DataType = "Control",
                        IsRequired = true,
                        Description = "Execution input."
                    }
                },
                OutputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "Next",
                        DisplayName = "Next",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Continues after the delay."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Error",
                        DisplayName = "Error",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes invalid delay configuration."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "DelayMs",
                        DisplayName = "Delay (ms)",
                        DataType = "Int32",
                        DefaultValue = 0,
                        IsRequired = true,
                        Description = "Delay duration in milliseconds."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "DelayMs",
                        DisplayName = "Delay (ms)",
                        DataType = "Int32",
                        Description = "The resolved delay duration."
                    }
                }
            };
        }
    }
}
