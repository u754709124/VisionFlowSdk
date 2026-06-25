using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    public sealed class CameraParameterSetConfig
    {
        public string Name { get; set; }

        public object Value { get; set; }

        public string ValueBinding { get; set; }
    }

    public sealed class CameraSetParameterNodeConfig
    {
        public CameraSetParameterNodeConfig()
        {
            TimeoutMs = 1000;
            Parameters = new List<CameraParameterSetConfig>();
        }

        public string CameraId { get; set; }

        public int TimeoutMs { get; set; }

        public IList<CameraParameterSetConfig> Parameters { get; set; }
    }

    public sealed class CameraSetParameterNodeFactory : BaseNodeFactory<CameraSetParameterNodeConfig>
    {
        public const string TypeName = "camera.set_parameters";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return CameraSetParameterNodeDescriptor.Create(); }
        }

        protected override CameraSetParameterNodeConfig CreateConfig(NodeDefinition definition)
        {
            var config = new CameraSetParameterNodeConfig
            {
                CameraId = GetStringSetting(definition, "CameraId", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 1000)
            };

            AddParameters(config.Parameters, GetSetting(definition, "Parameters", null));

            var parameterName = GetStringSetting(definition, "ParameterName", null);
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                config.Parameters.Add(new CameraParameterSetConfig
                {
                    Name = parameterName,
                    Value = GetSetting(definition, "Value", null),
                    ValueBinding = GetStringSetting(definition, "ValueBinding", null)
                });
            }

            return config;
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, CameraSetParameterNodeConfig config)
        {
            return new CameraSetParameterNode(config);
        }

        private static void AddParameters(IList<CameraParameterSetConfig> parameters, object value)
        {
            if (parameters == null || value == null)
            {
                return;
            }

            var typed = value as CameraParameterSetConfig;
            if (typed != null)
            {
                parameters.Add(CloneParameter(typed));
                return;
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                if (LooksLikeParameterItem(dictionary))
                {
                    parameters.Add(CreateParameterFromDictionary(dictionary));
                    return;
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    parameters.Add(new CameraParameterSetConfig
                    {
                        Name = Convert.ToString(entry.Key, CultureInfo.InvariantCulture),
                        Value = entry.Value
                    });
                }

                return;
            }

            if (value is string)
            {
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (var item in enumerable)
            {
                AddParameters(parameters, item);
            }
        }

        private static CameraParameterSetConfig CloneParameter(CameraParameterSetConfig parameter)
        {
            return new CameraParameterSetConfig
            {
                Name = parameter.Name,
                Value = parameter.Value,
                ValueBinding = parameter.ValueBinding
            };
        }

        private static bool LooksLikeParameterItem(IDictionary dictionary)
        {
            return ContainsKey(dictionary, "Name") ||
                ContainsKey(dictionary, "ParameterName") ||
                ContainsKey(dictionary, "Value") ||
                ContainsKey(dictionary, "ConstantValue") ||
                ContainsKey(dictionary, "ValueBinding") ||
                ContainsKey(dictionary, "Binding") ||
                ContainsKey(dictionary, "Expression");
        }

        private static CameraParameterSetConfig CreateParameterFromDictionary(IDictionary dictionary)
        {
            object name;
            if (!TryGetValue(dictionary, "Name", out name))
            {
                TryGetValue(dictionary, "ParameterName", out name);
            }

            object value;
            if (!TryGetValue(dictionary, "Value", out value))
            {
                TryGetValue(dictionary, "ConstantValue", out value);
            }

            object binding;
            if (!TryGetValue(dictionary, "ValueBinding", out binding) &&
                !TryGetValue(dictionary, "Binding", out binding))
            {
                TryGetValue(dictionary, "Expression", out binding);
            }

            return new CameraParameterSetConfig
            {
                Name = name == null ? null : Convert.ToString(name, CultureInfo.InvariantCulture),
                Value = value,
                ValueBinding = binding == null ? null : Convert.ToString(binding, CultureInfo.InvariantCulture)
            };
        }

        private static bool ContainsKey(IDictionary dictionary, string key)
        {
            object value;
            return TryGetValue(dictionary, key, out value);
        }

        private static bool TryGetValue(IDictionary dictionary, string key, out object value)
        {
            value = null;
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (DictionaryEntry entry in dictionary)
            {
                if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class CameraSetParameterNode : IFlowNode
    {
        private readonly CameraSetParameterNodeConfig _config;

        public CameraSetParameterNode(CameraSetParameterNodeConfig config)
        {
            _config = config ?? new CameraSetParameterNodeConfig();
            if (_config.Parameters == null)
            {
                _config.Parameters = new List<CameraParameterSetConfig>();
            }
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cameraId = CameraNodeHelpers.ResolveString(context, "CameraId", _config.CameraId);
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return NodeExecutionResult.Failure("CameraId is required.");
            }

            var timeoutMs = CameraNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            if (_config.Parameters.Count == 0)
            {
                return NodeExecutionResult.Failure("At least one camera parameter is required.");
            }

            var camera = context.Devices.GetCamera(cameraId);
            var appliedParameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            string currentParameter = null;

            using (var timeout = CameraNodeTimeout.Create(timeoutMs, cancellationToken))
            {
                try
                {
                    foreach (var parameter in _config.Parameters)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                        {
                            return NodeExecutionResult.Failure("Camera parameter name is required.");
                        }

                        currentParameter = parameter.Name;
                        var value = ResolveParameterValue(context, parameter);
                        await camera.SetParameterAsync(parameter.Name, value, timeout.Token).ConfigureAwait(false);
                        appliedParameters[parameter.Name] = value;
                    }
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    var message = string.IsNullOrWhiteSpace(currentParameter)
                        ? "Timed out setting camera parameters."
                        : "Timed out setting camera parameter: " + currentParameter;
                    return NodeExecutionResult.Timeout(message, "Timeout");
                }
            }

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "CameraId", cameraId },
                    { "ParameterCount", appliedParameters.Count },
                    { "Parameters", appliedParameters }
                });
        }

        private static object ResolveParameterValue(FlowExecutionContext context, CameraParameterSetConfig parameter)
        {
            object value;
            if (TryResolveParameterBinding(context, parameter.Name, out value))
            {
                return value;
            }

            if (!string.IsNullOrWhiteSpace(parameter.ValueBinding))
            {
                return context.ResolveBinding(VariableBinding.ForExpression(parameter.ValueBinding));
            }

            return parameter.Value;
        }

        private static bool TryResolveParameterBinding(FlowExecutionContext context, string parameterName, out object value)
        {
            value = null;
            if (context == null ||
                context.Node == null ||
                context.Node.InputBindings == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            var keys = new[]
            {
                parameterName,
                "Parameter." + parameterName,
                "Parameters." + parameterName
            };

            for (var index = 0; index < keys.Length; index++)
            {
                if (context.Node.InputBindings.ContainsKey(keys[index]))
                {
                    value = context.GetInputValue(keys[index]);
                    return true;
                }
            }

            return false;
        }
    }

    public static class CameraSetParameterNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = CameraSetParameterNodeFactory.TypeName,
                DisplayName = "Set Camera Parameters",
                Category = "Camera",
                Version = "1.0.0",
                Description = "Sets one or more camera parameters through an adapter.",
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
                        Description = "Continues after all parameters are applied."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Timeout",
                        DisplayName = "Timeout",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes device operation timeout."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Error",
                        DisplayName = "Error",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes invalid configuration or adapter errors."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered camera adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "Parameters",
                        DisplayName = "Parameters",
                        DataType = "Object",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Parameter assignments with Name, Value, and optional ValueBinding."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TimeoutMs",
                        DisplayName = "Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 1000,
                        IsRequired = true,
                        Description = "Maximum time for the parameter operation. Zero disables the node timeout."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        Description = "The resolved camera id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ParameterCount",
                        DisplayName = "Parameter Count",
                        DataType = "Int32",
                        Description = "Number of parameters applied."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Parameters",
                        DisplayName = "Parameters",
                        DataType = "Object",
                        Description = "Applied parameter values keyed by parameter name."
                    }
                }
            };
        }
    }

    public sealed class CameraSoftTriggerNodeConfig
    {
        public CameraSoftTriggerNodeConfig()
        {
            TimeoutMs = 1000;
        }

        public string CameraId { get; set; }

        public int TimeoutMs { get; set; }
    }

    public sealed class CameraSoftTriggerNodeFactory : BaseNodeFactory<CameraSoftTriggerNodeConfig>
    {
        public const string TypeName = "camera.soft_trigger";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return CameraSoftTriggerNodeDescriptor.Create(); }
        }

        protected override CameraSoftTriggerNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new CameraSoftTriggerNodeConfig
            {
                CameraId = GetStringSetting(definition, "CameraId", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 1000)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, CameraSoftTriggerNodeConfig config)
        {
            return new CameraSoftTriggerNode(config);
        }
    }

    public sealed class CameraSoftTriggerNode : IFlowNode
    {
        private readonly CameraSoftTriggerNodeConfig _config;

        public CameraSoftTriggerNode(CameraSoftTriggerNodeConfig config)
        {
            _config = config ?? new CameraSoftTriggerNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cameraId = CameraNodeHelpers.ResolveString(context, "CameraId", _config.CameraId);
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return NodeExecutionResult.Failure("CameraId is required.");
            }

            var timeoutMs = CameraNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var camera = context.Devices.GetCamera(cameraId);
            var triggerId = Guid.NewGuid().ToString("N");
            var triggerTime = DateTime.UtcNow;
            var triggerContext = new CameraTriggerContext
            {
                CameraId = cameraId,
                TriggerId = triggerId,
                Token = context.Token
            };
            triggerContext.Metadata["CameraId"] = cameraId;
            triggerContext.Metadata["TriggerId"] = triggerId;
            triggerContext.Metadata["TriggerTime"] = triggerTime;

            context.CameraFrames.EnsureCamera(camera, cameraId);

            using (var timeout = CameraNodeTimeout.Create(timeoutMs, cancellationToken))
            {
                try
                {
                    await camera.SoftTriggerAsync(triggerContext, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    return NodeExecutionResult.Timeout("Timed out executing camera soft trigger.", "Timeout");
                }
            }

            context.Token.Set("CameraId", cameraId);
            context.Token.Set("TriggerId", triggerId);
            context.Token.Set("TriggerTime", triggerTime);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "CameraId", cameraId },
                    { "TriggerId", triggerId },
                    { "TriggerTime", triggerTime }
                });
        }
    }

    public static class CameraSoftTriggerNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = CameraSoftTriggerNodeFactory.TypeName,
                DisplayName = "Camera Soft Trigger",
                Category = "Camera",
                Version = "1.0.0",
                Description = "Issues a software trigger through a camera adapter without waiting for an image.",
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
                        Description = "Continues immediately after the soft trigger is accepted."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Timeout",
                        DisplayName = "Timeout",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes device operation timeout."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Error",
                        DisplayName = "Error",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes invalid configuration or adapter errors."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered camera adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TimeoutMs",
                        DisplayName = "Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 1000,
                        IsRequired = true,
                        Description = "Maximum time for the trigger command. Zero disables the node timeout."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        Description = "The resolved camera id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "TriggerId",
                        DisplayName = "Trigger Id",
                        DataType = "String",
                        Description = "Generated trigger id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "TriggerTime",
                        DisplayName = "Trigger Time",
                        DataType = "DateTime",
                        Description = "UTC time when the trigger command was issued."
                    }
                }
            };
        }
    }

    public sealed class CameraImageCallbackNodeConfig
    {
        public CameraImageCallbackNodeConfig()
        {
            TimeoutMs = 1000;
            FrameTimeoutMs = 1000;
            ExpectedFrameCount = 1;
            CallbackMode = "WaitNextFrame";
            MatchMode = "TriggerId";
        }

        public string CameraId { get; set; }

        public string TriggerId { get; set; }

        public string CallbackMode { get; set; }

        public string MatchMode { get; set; }

        public string ScanGroupIdBinding { get; set; }

        public int TimeoutMs { get; set; }

        public int ExpectedFrameCount { get; set; }

        public int FrameTimeoutMs { get; set; }
    }

    public sealed class CameraImageCallbackNodeFactory : BaseNodeFactory<CameraImageCallbackNodeConfig>
    {
        public const string TypeName = "camera.image_callback";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return CameraImageCallbackNodeDescriptor.Create(); }
        }

        protected override CameraImageCallbackNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new CameraImageCallbackNodeConfig
            {
                CameraId = GetStringSetting(definition, "CameraId", null),
                TriggerId = GetStringSetting(definition, "TriggerId", null),
                CallbackMode = GetStringSetting(definition, "CallbackMode", "WaitNextFrame"),
                MatchMode = GetStringSetting(definition, "MatchMode", "TriggerId"),
                ScanGroupIdBinding = GetStringSetting(definition, "ScanGroupIdBinding", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 1000),
                ExpectedFrameCount = GetInt32Setting(definition, "ExpectedFrameCount", 1),
                FrameTimeoutMs = GetInt32Setting(definition, "FrameTimeoutMs", 1000)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, CameraImageCallbackNodeConfig config)
        {
            return new CameraImageCallbackNode(config);
        }
    }

    public sealed class CameraImageCallbackNode : IFlowNode
    {
        private readonly CameraImageCallbackNodeConfig _config;

        public CameraImageCallbackNode(CameraImageCallbackNodeConfig config)
        {
            _config = config ?? new CameraImageCallbackNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cameraId = CameraNodeHelpers.ResolveString(context, "CameraId", _config.CameraId);
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return NodeExecutionResult.Failure("CameraId is required.");
            }

            var timeoutMs = CameraNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var callbackMode = CameraNodeHelpers.ResolveString(context, "CallbackMode", _config.CallbackMode);
            if (string.IsNullOrWhiteSpace(callbackMode))
            {
                callbackMode = "WaitNextFrame";
            }

            var matchMode = CameraNodeHelpers.ResolveString(context, "MatchMode", _config.MatchMode);
            if (string.IsNullOrWhiteSpace(matchMode))
            {
                matchMode = "TriggerId";
            }

            var camera = context.Devices.GetCamera(cameraId);
            context.CameraFrames.EnsureCamera(camera, cameraId);

            var ticket = CreateWaitTicket(context, cameraId, matchMode);
            if (ticket == null)
            {
                return NodeExecutionResult.Failure("CameraImageCallbackNode supports TriggerId, Any, and ScanGroupId match modes.");
            }

            if (string.Equals(callbackMode, "StreamFrames", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteStreamFramesAsync(context, camera, ticket, timeoutMs, cancellationToken).ConfigureAwait(false);
            }

            if (!string.Equals(callbackMode, "WaitNextFrame", StringComparison.OrdinalIgnoreCase))
            {
                return NodeExecutionResult.Failure("Unsupported camera callback mode: " + callbackMode);
            }

            CameraFrameData frame;
            try
            {
                frame = await context.CameraFrames.WaitForFrameAsync(
                    camera,
                    ticket,
                    timeoutMs,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (frame == null)
            {
                return NodeExecutionResult.Timeout(
                    "Timed out waiting for camera frame. CameraId=" + cameraId + ", " + ticket.Describe(),
                    "Timeout");
            }

            return CreateFrameResult(context, frame, null);
        }

        private async Task<NodeExecutionResult> ExecuteStreamFramesAsync(
            FlowExecutionContext context,
            ICameraAdapter camera,
            CameraFrameWaitTicket ticket,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var expectedFrameCount = CameraNodeHelpers.ResolveInt32(context, "ExpectedFrameCount", _config.ExpectedFrameCount);
            if (expectedFrameCount <= 0)
            {
                return NodeExecutionResult.Failure("ExpectedFrameCount must be greater than zero.");
            }

            var frameTimeoutMs = CameraNodeHelpers.ResolveInt32(context, "FrameTimeoutMs", _config.FrameTimeoutMs);
            if (frameTimeoutMs < 0)
            {
                return NodeExecutionResult.Failure("FrameTimeoutMs must be greater than or equal to zero.");
            }

            if (frameTimeoutMs == 0)
            {
                frameTimeoutMs = timeoutMs;
            }

            var frames = new List<CameraFrameData>();
            using (var subscription = context.CameraFrames.Subscribe(camera, ticket))
            {
                for (var index = 0; index < expectedFrameCount; index++)
                {
                    var frame = await subscription.WaitForNextFrameAsync(frameTimeoutMs, cancellationToken).ConfigureAwait(false);
                    if (frame == null)
                    {
                        return NodeExecutionResult.Timeout(
                            "Timed out waiting for camera stream frame. CameraId=" + ticket.CameraId + ", " + ticket.Describe(),
                            "Timeout");
                    }

                    frames.Add(frame);
                }
            }

            return CreateFrameResult(context, frames[frames.Count - 1], frames);
        }

        private CameraFrameWaitTicket CreateWaitTicket(FlowExecutionContext context, string cameraId, string matchMode)
        {
            if (string.Equals(matchMode, CameraFrameMatchModes.TriggerId, StringComparison.OrdinalIgnoreCase))
            {
                var triggerId = ResolveTriggerId(context);
                if (string.IsNullOrWhiteSpace(triggerId))
                {
                    throw new InvalidOperationException("TriggerId is required for camera image callback matching.");
                }

                return new CameraFrameWaitTicket
                {
                    CameraId = cameraId,
                    MatchMode = CameraFrameMatchModes.TriggerId,
                    TriggerId = triggerId
                };
            }

            if (string.Equals(matchMode, CameraFrameMatchModes.Any, StringComparison.OrdinalIgnoreCase))
            {
                return new CameraFrameWaitTicket
                {
                    CameraId = cameraId,
                    MatchMode = CameraFrameMatchModes.Any
                };
            }

            if (string.Equals(matchMode, CameraFrameMatchModes.ScanGroupId, StringComparison.OrdinalIgnoreCase))
            {
                var scanGroupId = ResolveScanGroupId(context);
                if (string.IsNullOrWhiteSpace(scanGroupId))
                {
                    throw new InvalidOperationException("ScanGroupIdBinding must resolve to a value when MatchMode is ScanGroupId.");
                }

                return new CameraFrameWaitTicket
                {
                    CameraId = cameraId,
                    MatchMode = CameraFrameMatchModes.ScanGroupId,
                    ScanGroupId = scanGroupId
                };
            }

            return null;
        }

        private NodeExecutionResult CreateFrameResult(
            FlowExecutionContext context,
            CameraFrameData frame,
            IList<CameraFrameData> frames)
        {
            if (!string.IsNullOrWhiteSpace(frame.FrameId))
            {
                context.Token.FrameId = frame.FrameId;
                context.Token.Set("FrameId", frame.FrameId);
            }

            var metadata = frame.Metadata ?? new Dictionary<string, object>();
            var outputs = new Dictionary<string, object>
            {
                { "Image", frame.Image },
                { "Frame", frame },
                { "FrameId", frame.FrameId },
                { "GrabTime", frame.GrabTime },
                { "Metadata", metadata },
                { "CameraId", frame.CameraId },
                { "TriggerId", frame.TriggerId }
            };

            if (frames != null)
            {
                outputs["Frames"] = frames;
                outputs["FrameCount"] = frames.Count;
            }

            return NodeExecutionResult.Success("Next", outputs);
        }

        private string ResolveScanGroupId(FlowExecutionContext context)
        {
            var binding = CameraNodeHelpers.ResolveString(context, "ScanGroupIdBinding", _config.ScanGroupIdBinding);
            if (!string.IsNullOrWhiteSpace(binding))
            {
                var value = ControlFlowNodeHelpers.ResolveBindingExpression(context, binding);
                return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            var inputValue = context.GetInputValue("ScanGroupId");
            if (inputValue != null)
            {
                return Convert.ToString(inputValue, CultureInfo.InvariantCulture);
            }

            return string.IsNullOrWhiteSpace(context.Token.ScanGroupId) ? null : context.Token.ScanGroupId;
        }

        private string ResolveTriggerId(FlowExecutionContext context)
        {
            var value = context.GetInputValue("TriggerId");
            var triggerId = value == null ? _config.TriggerId : Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(triggerId))
            {
                return triggerId;
            }

            string tokenTriggerId;
            if (context.Token.TryGet("TriggerId", out tokenTriggerId))
            {
                return tokenTriggerId;
            }

            return null;
        }
    }

    public static class CameraImageCallbackNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = CameraImageCallbackNodeFactory.TypeName,
                DisplayName = "Camera Image Callback",
                Category = "Camera",
                Version = "1.0.0",
                Description = "Waits for a frame matching the trigger id from a camera adapter.",
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
                        Description = "Continues when a matching frame is received."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Timeout",
                        DisplayName = "Timeout",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes frame wait timeout."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Error",
                        DisplayName = "Error",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes invalid configuration or adapter errors."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered camera adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "CallbackMode",
                        DisplayName = "Callback Mode",
                        DataType = "String",
                        DefaultValue = "WaitNextFrame",
                        IsRequired = false,
                        Description = "WaitNextFrame waits for one frame. StreamFrames subscribes and collects one or more frames."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TriggerId",
                        DisplayName = "Trigger Id",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Trigger id to match. This can also come from a binding or token value."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "MatchMode",
                        DisplayName = "Match Mode",
                        DataType = "String",
                        DefaultValue = "TriggerId",
                        IsRequired = true,
                        Description = "Frame matching mode: TriggerId, Any, or ScanGroupId."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "ScanGroupIdBinding",
                        DisplayName = "Scan Group Binding",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Expression used when MatchMode is ScanGroupId, for example {{ token.ScanGroupId }}."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TimeoutMs",
                        DisplayName = "Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 1000,
                        IsRequired = true,
                        Description = "Maximum time to wait for the frame. Zero disables the node timeout."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "ExpectedFrameCount",
                        DisplayName = "Expected Frames",
                        DataType = "Int32",
                        DefaultValue = 1,
                        IsRequired = false,
                        Description = "Number of frames to collect when CallbackMode is StreamFrames."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "FrameTimeoutMs",
                        DisplayName = "Frame Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 1000,
                        IsRequired = false,
                        Description = "Per-frame timeout when CallbackMode is StreamFrames. Zero uses TimeoutMs."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "Image",
                        DisplayName = "Image",
                        DataType = "IVisionImage",
                        Description = "Captured image."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Frame",
                        DisplayName = "Frame",
                        DataType = "CameraFrameData",
                        Description = "Complete frame data."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "FrameId",
                        DisplayName = "Frame Id",
                        DataType = "String",
                        Description = "Captured frame id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "GrabTime",
                        DisplayName = "Grab Time",
                        DataType = "DateTime",
                        Description = "UTC image grab time."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Metadata",
                        DisplayName = "Metadata",
                        DataType = "Object",
                        Description = "Frame metadata."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        Description = "Frame camera id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "TriggerId",
                        DisplayName = "Trigger Id",
                        DataType = "String",
                        Description = "Frame trigger id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Frames",
                        DisplayName = "Frames",
                        DataType = "Object",
                        Description = "Collected frames when CallbackMode is StreamFrames."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "FrameCount",
                        DisplayName = "Frame Count",
                        DataType = "Int32",
                        Description = "Number of collected frames when CallbackMode is StreamFrames."
                    }
                }
            };
        }
    }

    internal static class CameraNodeHelpers
    {
        public static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = context.GetInputValue(name);
            return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
        {
            var value = context.GetInputValue(name);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
    }

    internal sealed class CameraNodeTimeout : IDisposable
    {
        private readonly CancellationTokenSource _source;

        private CameraNodeTimeout(CancellationTokenSource source)
        {
            _source = source;
        }

        public CancellationToken Token
        {
            get { return _source.Token; }
        }

        public static CameraNodeTimeout Create(int timeoutMs, CancellationToken cancellationToken)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMs > 0)
            {
                source.CancelAfter(timeoutMs);
            }

            return new CameraNodeTimeout(source);
        }

        public void Dispose()
        {
            _source.Dispose();
        }
    }

}
