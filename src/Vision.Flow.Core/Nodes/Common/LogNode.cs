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
    public sealed class LogNodeConfig
    {
        public LogNodeConfig()
        {
            Level = "Info";
            Message = string.Empty;
        }

        public string Level { get; set; }

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
                Level = GetStringSetting(definition, "Level", "Info"),
                Message = GetStringSetting(definition, "Message", string.Empty)
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

            var level = ResolveString(context, "Level", _config.Level);
            if (string.IsNullOrWhiteSpace(level))
            {
                level = "Info";
            }

            var message = ResolveString(context, "Message", _config.Message);
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
                "Next");
            runtimeEvent.Data[FlowRuntimeDataKeys.Kind] = "Log";
            runtimeEvent.Data[FlowRuntimeDataKeys.LogLevel] = level;
            runtimeEvent.Data[FlowRuntimeDataKeys.Message] = message;
            await context.Events.PublishAsync(runtimeEvent, cancellationToken).ConfigureAwait(false);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "Level", level },
                    { "Message", message }
                });
        }

        private static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = context.GetInputValue(name);
            return value == null ? defaultValue : Convert.ToString(value);
        }
    }

    public static class LogNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = LogNodeFactory.TypeName,
                DisplayName = "Write Log",
                Category = "Common",
                Version = "1.0.0",
                Description = "Publishes a runtime log event.",
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
                        Description = "Continues after publishing the log."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "Level",
                        DisplayName = "Level",
                        DataType = "String",
                        DefaultValue = "Info",
                        IsRequired = false,
                        Description = "Log level, such as Info, Warning, or Error."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "Message",
                        DisplayName = "Message",
                        DataType = "String",
                        DefaultValue = string.Empty,
                        IsRequired = false,
                        Description = "Message to publish."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "Level",
                        DisplayName = "Level",
                        DataType = "String",
                        Description = "The resolved log level."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Message",
                        DisplayName = "Message",
                        DataType = "String",
                        Description = "The resolved log message."
                    }
                }
            };
        }
    }
}
