using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Execution;

namespace Vision.Flow.Nodes
{
    public sealed class LogNodeConfig
    {
        public LogNodeConfig()
        {
            Level = FlowLogLevel.Info;
            Message = string.Empty;
        }

        public FlowLogLevel Level { get; set; }

        public string Message { get; set; }
    }

    public sealed class LogNodeFactory : BaseNodeFactory<LogNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.LogWrite;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return LogNodeDescriptor.Create(); }
        }

        protected override LogNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new LogNodeConfig
            {
                Level = GetEnumSetting(definition, FlowSettingNames.Level, FlowLogLevel.Info),
                Message = GetStringSetting(definition, FlowSettingNames.Message, string.Empty)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, LogNodeConfig config)
        {
            return new LogNode(config);
        }
    }

    public sealed class LogNode : IFlowNode
    {
        private readonly LogNodeConfig _config;

        public LogNode(LogNodeConfig config)
        {
            _config = config ?? new LogNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var level = ResolveEnum(context, FlowSettingNames.Level, _config.Level);
            var levelText = FlowEnumConverter.ToWireValue(level);

            var message = ResolveString(context, FlowSettingNames.Message, _config.Message);
            if (message == null)
            {
                message = string.Empty;
            }

            var runtimeEvent = FlowRuntimeEvent.Create(
                FlowRuntimeEventType.NodeCompleted,
                context.Flow,
                context.Token,
                context.Node,
                NodeRuntimeState.Completed,
                message,
                FlowPortNames.Next);
            runtimeEvent.Data[FlowRuntimeDataKeys.Kind] = "Log";
            runtimeEvent.Data[FlowRuntimeDataKeys.LogLevel] = levelText;
            runtimeEvent.Data[FlowRuntimeDataKeys.Message] = message;
            await context.Events.PublishAsync(runtimeEvent, cancellationToken).ConfigureAwait(false);

            return NodeExecutionResult.Success(
                FlowPortNames.Next,
                new Dictionary<string, object>
                {
                    { FlowSettingNames.Level, levelText },
                    { FlowSettingNames.Message, message }
                });
        }

        private static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = context.GetInputValue(name);
            return value == null ? defaultValue : Convert.ToString(value);
        }

        private static FlowLogLevel ResolveEnum(FlowExecutionContext context, string name, FlowLogLevel defaultValue)
        {
            var value = context.GetInputValue(name);
            return FlowEnumConverter.ParseOrDefault(value, defaultValue);
        }
    }

    public static class LogNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = LogNodeFactory.TypeName,
                DisplayName = "写日志",
                Category = "通用",
                Version = "1.0.0",
                Description = "发布一条运行时日志事件。",
                InputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "In",
                        DisplayName = "In",
                        Direction = FlowPortDirection.Input,
                        DataType = FlowDataType.Control,
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
                        Direction = FlowPortDirection.Output,
                        DataType = FlowDataType.Control,
                        Description = "Continues after publishing the log."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = FlowSettingNames.Level,
                        DisplayName = "Level",
                        DataType = FlowDataType.String,
                        DefaultValue = FlowEnumConverter.ToWireValue(FlowLogLevel.Info),
                        IsRequired = false,
                        Description = "Log level, such as Info, Warning, or Error."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = FlowSettingNames.Message,
                        DisplayName = "Message",
                        DataType = FlowDataType.String,
                        DefaultValue = string.Empty,
                        IsRequired = false,
                        Description = "Message to publish."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = FlowSettingNames.Level,
                        DisplayName = "Level",
                        DataType = FlowDataType.String,
                        Description = "The resolved log level."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = FlowSettingNames.Message,
                        DisplayName = "Message",
                        DataType = FlowDataType.String,
                        Description = "The resolved log message."
                    }
                }
            };
        }
    }
}
