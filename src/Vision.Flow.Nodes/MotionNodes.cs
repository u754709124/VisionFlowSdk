using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    public sealed class MotionNotifyNodeConfig
    {
        public MotionNotifyNodeConfig()
        {
            TimeoutMs = 1000;
        }

        public string MotionId { get; set; }

        public string MessageType { get; set; }

        public string PositionIdBinding { get; set; }

        public string CaptureGroupIdBinding { get; set; }

        public string ScanGroupIdBinding { get; set; }

        public string ResultBinding { get; set; }

        public int TimeoutMs { get; set; }
    }

    public sealed class MotionNotifyNodeFactory : BaseNodeFactory<MotionNotifyNodeConfig>
    {
        public const string TypeName = "motion.notify";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return MotionNotifyNodeDescriptor.Create(); }
        }

        protected override MotionNotifyNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new MotionNotifyNodeConfig
            {
                MotionId = GetStringSetting(definition, "MotionId", null),
                MessageType = GetStringSetting(definition, "MessageType", null),
                PositionIdBinding = GetStringSetting(definition, "PositionIdBinding", null),
                CaptureGroupIdBinding = GetStringSetting(definition, "CaptureGroupIdBinding", null),
                ScanGroupIdBinding = GetStringSetting(definition, "ScanGroupIdBinding", null),
                ResultBinding = GetStringSetting(definition, "ResultBinding", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 1000)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, MotionNotifyNodeConfig config)
        {
            return new MotionNotifyNode(config);
        }
    }

    public sealed class MotionNotifyNode : IFlowNode
    {
        private readonly MotionNotifyNodeConfig _config;

        public MotionNotifyNode(MotionNotifyNodeConfig config)
        {
            _config = config ?? new MotionNotifyNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var motionId = AdapterNodeHelpers.ResolveString(context, "MotionId", _config.MotionId);
            if (string.IsNullOrWhiteSpace(motionId))
            {
                return NodeExecutionResult.Failure("MotionId is required.");
            }

            var messageType = AdapterNodeHelpers.ResolveString(context, "MessageType", _config.MessageType);
            if (string.IsNullOrWhiteSpace(messageType))
            {
                return NodeExecutionResult.Failure("MessageType is required.");
            }

            var timeoutMs = AdapterNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var message = new MotionMessage
            {
                MotionId = motionId,
                MessageType = messageType,
                TokenId = context.Token.TokenId,
                PositionId = MotionNodeHelpers.ResolveStringBinding(context, _config.PositionIdBinding, context.Token.PositionId),
                CaptureGroupId = MotionNodeHelpers.ResolveStringBinding(context, _config.CaptureGroupIdBinding, context.Token.CaptureGroupId),
                ScanGroupId = MotionNodeHelpers.ResolveStringBinding(context, _config.ScanGroupIdBinding, context.Token.ScanGroupId),
                Result = MotionNodeHelpers.ResolveObjectBinding(context, _config.ResultBinding, null)
            };
            message.Metadata["TokenId"] = context.Token.TokenId;

            var motion = context.Devices.GetMotion(motionId);
            var completed = await MotionNodeHelpers.ExecuteWithTimeoutAsync(
                delegate(CancellationToken token)
                {
                    return motion.SendMessageAsync(message, token);
                },
                timeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (!completed)
            {
                return NodeExecutionResult.Timeout("Timed out sending motion message.", "Timeout");
            }

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "MotionId", motionId },
                    { "MessageType", messageType },
                    { "Message", message },
                    { "PositionId", message.PositionId },
                    { "CaptureGroupId", message.CaptureGroupId },
                    { "ScanGroupId", message.ScanGroupId },
                    { "Result", message.Result }
                });
        }
    }

    public sealed class MotionMoveToNodeConfig
    {
        public MotionMoveToNodeConfig()
        {
            TimeoutMs = 5000;
        }

        public string MotionId { get; set; }

        public string PositionName { get; set; }

        public int TimeoutMs { get; set; }
    }

    public sealed class MotionMoveToNodeFactory : BaseNodeFactory<MotionMoveToNodeConfig>
    {
        public const string TypeName = "motion.move_to";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return MotionMoveToNodeDescriptor.Create(); }
        }

        protected override MotionMoveToNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new MotionMoveToNodeConfig
            {
                MotionId = GetStringSetting(definition, "MotionId", null),
                PositionName = GetStringSetting(definition, "PositionName", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 5000)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, MotionMoveToNodeConfig config)
        {
            return new MotionMoveToNode(config);
        }
    }

    public sealed class MotionMoveToNode : IFlowNode
    {
        private readonly MotionMoveToNodeConfig _config;

        public MotionMoveToNode(MotionMoveToNodeConfig config)
        {
            _config = config ?? new MotionMoveToNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var motionId = AdapterNodeHelpers.ResolveString(context, "MotionId", _config.MotionId);
            if (string.IsNullOrWhiteSpace(motionId))
            {
                return NodeExecutionResult.Failure("MotionId is required.");
            }

            var positionName = AdapterNodeHelpers.ResolveString(context, "PositionName", _config.PositionName);
            if (string.IsNullOrWhiteSpace(positionName))
            {
                return NodeExecutionResult.Failure("PositionName is required.");
            }

            var timeoutMs = AdapterNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var motion = context.Devices.GetMotion(motionId);
            var completed = await MotionNodeHelpers.ExecuteWithTimeoutAsync(
                delegate(CancellationToken token)
                {
                    return motion.MoveToAsync(positionName, token);
                },
                timeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (!completed)
            {
                return NodeExecutionResult.Timeout("Timed out moving motion adapter.", "Timeout");
            }

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "MotionId", motionId },
                    { "PositionName", positionName }
                });
        }
    }

    public sealed class MotionWaitInPositionNodeConfig
    {
        public MotionWaitInPositionNodeConfig()
        {
            TimeoutMs = 5000;
        }

        public string MotionId { get; set; }

        public string PositionName { get; set; }

        public int TimeoutMs { get; set; }
    }

    public sealed class MotionWaitInPositionNodeFactory : BaseNodeFactory<MotionWaitInPositionNodeConfig>
    {
        public const string TypeName = "motion.wait_in_position";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return MotionWaitInPositionNodeDescriptor.Create(); }
        }

        protected override MotionWaitInPositionNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new MotionWaitInPositionNodeConfig
            {
                MotionId = GetStringSetting(definition, "MotionId", null),
                PositionName = GetStringSetting(definition, "PositionName", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 5000)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, MotionWaitInPositionNodeConfig config)
        {
            return new MotionWaitInPositionNode(config);
        }
    }

    public sealed class MotionWaitInPositionNode : IFlowNode
    {
        private readonly MotionWaitInPositionNodeConfig _config;

        public MotionWaitInPositionNode(MotionWaitInPositionNodeConfig config)
        {
            _config = config ?? new MotionWaitInPositionNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var motionId = AdapterNodeHelpers.ResolveString(context, "MotionId", _config.MotionId);
            if (string.IsNullOrWhiteSpace(motionId))
            {
                return NodeExecutionResult.Failure("MotionId is required.");
            }

            var positionName = AdapterNodeHelpers.ResolveString(context, "PositionName", _config.PositionName);
            if (string.IsNullOrWhiteSpace(positionName))
            {
                return NodeExecutionResult.Failure("PositionName is required.");
            }

            var timeoutMs = AdapterNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var motion = context.Devices.GetMotion(motionId);
            var completed = await MotionNodeHelpers.ExecuteWithTimeoutAsync(
                delegate(CancellationToken token)
                {
                    return motion.WaitForInPositionAsync(positionName, token);
                },
                timeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (!completed)
            {
                return NodeExecutionResult.Timeout("Timed out waiting for motion in position.", "Timeout");
            }

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "MotionId", motionId },
                    { "PositionName", positionName }
                });
        }
    }

    public static class MotionNotifyNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = MotionNotifyNodeFactory.TypeName,
                DisplayName = "Motion Notify",
                Category = "Motion",
                Version = "1.0.0",
                Description = "Sends a handshake message to a motion adapter.",
                InputPorts = { AdapterNodeDescriptors.ControlIn() },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.TimeoutOut("Routes motion notification timeout."),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    MotionNodeDescriptors.StringSetting("MotionId", "Motion", null, true, "Motion adapter id."),
                    MotionNodeDescriptors.StringSetting("MessageType", "Message Type", null, true, "Message type, such as PhotoDone or InspectionDone."),
                    MotionNodeDescriptors.StringSetting("PositionIdBinding", "Position Binding", null, false, "Optional binding for PositionId."),
                    MotionNodeDescriptors.StringSetting("CaptureGroupIdBinding", "Capture Group Binding", null, false, "Optional binding for CaptureGroupId."),
                    MotionNodeDescriptors.StringSetting("ScanGroupIdBinding", "Scan Group Binding", null, false, "Optional binding for ScanGroupId."),
                    MotionNodeDescriptors.StringSetting("ResultBinding", "Result Binding", null, false, "Optional binding for result payload."),
                    MotionNodeDescriptors.IntSetting("TimeoutMs", "Timeout (ms)", 1000, false, "Timeout in milliseconds. Zero disables timeout handling.")
                },
                Outputs =
                {
                    MotionNodeDescriptors.Output("MotionId", "Motion", "String", "Motion adapter id."),
                    MotionNodeDescriptors.Output("MessageType", "Message Type", "String", "Sent message type."),
                    MotionNodeDescriptors.Output("Message", "Message", "MotionMessage", "Sent motion message."),
                    MotionNodeDescriptors.Output("PositionId", "Position", "String", "Resolved PositionId."),
                    MotionNodeDescriptors.Output("CaptureGroupId", "Capture Group", "String", "Resolved CaptureGroupId."),
                    MotionNodeDescriptors.Output("ScanGroupId", "Scan Group", "String", "Resolved ScanGroupId."),
                    MotionNodeDescriptors.Output("Result", "Result", "Object", "Resolved result payload.")
                }
            };
        }
    }

    public static class MotionMoveToNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return MotionNodeDescriptors.CreatePositionNodeDescriptor(
                MotionMoveToNodeFactory.TypeName,
                "Motion Move To",
                "Commands a motion adapter to move to a named position.");
        }
    }

    public static class MotionWaitInPositionNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return MotionNodeDescriptors.CreatePositionNodeDescriptor(
                MotionWaitInPositionNodeFactory.TypeName,
                "Motion Wait In Position",
                "Waits until a motion adapter reports a named position.");
        }
    }

    internal static class MotionNodeDescriptors
    {
        public static NodeDescriptor CreatePositionNodeDescriptor(string nodeType, string displayName, string description)
        {
            return new NodeDescriptor
            {
                NodeType = nodeType,
                DisplayName = displayName,
                Category = "Motion",
                Version = "1.0.0",
                Description = description,
                InputPorts = { AdapterNodeDescriptors.ControlIn() },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.TimeoutOut("Routes motion timeout."),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    StringSetting("MotionId", "Motion", null, true, "Motion adapter id."),
                    StringSetting("PositionName", "Position", null, true, "Motion position name."),
                    IntSetting("TimeoutMs", "Timeout (ms)", 5000, false, "Timeout in milliseconds. Zero disables timeout handling.")
                },
                Outputs =
                {
                    Output("MotionId", "Motion", "String", "Motion adapter id."),
                    Output("PositionName", "Position", "String", "Motion position name.")
                }
            };
        }

        public static NodeSettingDescriptor StringSetting(string name, string displayName, string defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "String",
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        public static NodeSettingDescriptor IntSetting(string name, string displayName, int defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "Int32",
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        public static NodeOutputDescriptor Output(string name, string displayName, string dataType, string description)
        {
            return new NodeOutputDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = dataType,
                Description = description
            };
        }
    }

    internal static class MotionNodeHelpers
    {
        public static async Task<bool> ExecuteWithTimeoutAsync(
            Func<CancellationToken, Task> action,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            if (timeoutMs <= 0)
            {
                await action(cancellationToken).ConfigureAwait(false);
                return true;
            }

            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var operation = action(linkedCancellation.Token);
                var delay = Task.Delay(timeoutMs, cancellationToken);
                var completed = await Task.WhenAny(operation, delay).ConfigureAwait(false);
                if (completed == operation)
                {
                    await operation.ConfigureAwait(false);
                    return true;
                }

                cancellationToken.ThrowIfCancellationRequested();
                linkedCancellation.Cancel();
                return false;
            }
        }

        public static string ResolveStringBinding(FlowExecutionContext context, string bindingExpression, string defaultValue)
        {
            var value = ResolveObjectBinding(context, bindingExpression, defaultValue);
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static object ResolveObjectBinding(FlowExecutionContext context, string bindingExpression, object defaultValue)
        {
            if (string.IsNullOrWhiteSpace(bindingExpression))
            {
                return defaultValue;
            }

            return ControlFlowNodeHelpers.ResolveBindingExpression(context, bindingExpression);
        }
    }
}
