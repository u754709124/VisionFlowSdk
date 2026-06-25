using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // Camera parameter nodes resolve configured values and apply them through ICameraAdapter.
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
}
