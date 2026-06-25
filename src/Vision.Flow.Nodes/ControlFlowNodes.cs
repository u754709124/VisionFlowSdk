using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    public sealed class AndJoinNodeConfig
    {
        public AndJoinNodeConfig()
        {
            ExpectedInputCount = 2;
            TimeoutMs = 0;
            DuplicatePolicy = "Ignore";
        }

        public string JoinKeyBinding { get; set; }

        public int ExpectedInputCount { get; set; }

        public int TimeoutMs { get; set; }

        public string DuplicatePolicy { get; set; }
    }

    public sealed class AndJoinNodeFactory : BaseNodeFactory<AndJoinNodeConfig>
    {
        public const string TypeName = "join.and";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return AndJoinNodeDescriptor.Create(); }
        }

        protected override AndJoinNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new AndJoinNodeConfig
            {
                JoinKeyBinding = GetStringSetting(definition, "JoinKeyBinding", null),
                ExpectedInputCount = GetInt32Setting(definition, "ExpectedInputCount", 2),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 0),
                DuplicatePolicy = GetStringSetting(definition, "DuplicatePolicy", "Ignore")
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, AndJoinNodeConfig config)
        {
            return new AndJoinNode(config);
        }
    }

    public sealed class AndJoinNode : IFlowNode
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, AndJoinBucket> _buckets = new Dictionary<string, AndJoinBucket>(StringComparer.OrdinalIgnoreCase);
        private readonly AndJoinNodeConfig _config;

        public AndJoinNode(AndJoinNodeConfig config)
        {
            _config = config ?? new AndJoinNodeConfig();
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var expectedInputCount = ControlFlowNodeHelpers.ResolveInt32(context, "ExpectedInputCount", _config.ExpectedInputCount);
            if (expectedInputCount <= 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("ExpectedInputCount must be greater than zero."));
            }

            var timeoutMs = ControlFlowNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return Task.FromResult(NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero."));
            }

            var joinKeyBinding = ControlFlowNodeHelpers.ResolveString(context, "JoinKeyBinding", _config.JoinKeyBinding);
            var joinKeyValue = ControlFlowNodeHelpers.ResolveBindingExpression(context, joinKeyBinding);
            var joinKey = joinKeyValue == null ? null : Convert.ToString(joinKeyValue, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(joinKey))
            {
                return Task.FromResult(NodeExecutionResult.Failure("JoinKeyBinding must resolve to a non-empty value."));
            }

            var duplicatePolicy = ControlFlowNodeHelpers.ResolveString(context, "DuplicatePolicy", _config.DuplicatePolicy);
            if (string.IsNullOrWhiteSpace(duplicatePolicy))
            {
                duplicatePolicy = "Ignore";
            }

            var inputKey = string.IsNullOrWhiteSpace(context.Token.TokenId)
                ? Guid.NewGuid().ToString("N")
                : context.Token.TokenId;

            lock (_gate)
            {
                AndJoinBucket bucket;
                if (!_buckets.TryGetValue(joinKey, out bucket))
                {
                    bucket = new AndJoinBucket(joinKey, expectedInputCount, timeoutMs);
                    _buckets[joinKey] = bucket;
                }

                bucket.ExpectedInputCount = expectedInputCount;
                bucket.TimeoutMs = timeoutMs;

                if (bucket.IsExpired())
                {
                    _buckets.Remove(joinKey);
                    return Task.FromResult(NodeExecutionResult.Timeout("AND join timed out for JoinKey: " + joinKey));
                }

                if (bucket.Inputs.ContainsKey(inputKey))
                {
                    if (string.Equals(duplicatePolicy, "Error", StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(NodeExecutionResult.Failure("Duplicate AND join input for JoinKey '" + joinKey + "' and TokenId '" + inputKey + "'."));
                    }

                    if (string.Equals(duplicatePolicy, "Replace", StringComparison.OrdinalIgnoreCase))
                    {
                        bucket.Inputs[inputKey] = DateTime.UtcNow;
                    }

                    return Task.FromResult(CreateWaitingResult(bucket));
                }

                bucket.Inputs[inputKey] = DateTime.UtcNow;
                if (bucket.Inputs.Count < expectedInputCount)
                {
                    return Task.FromResult(CreateWaitingResult(bucket));
                }

                _buckets.Remove(joinKey);
                return Task.FromResult(
                    NodeExecutionResult.Success(
                        "Next",
                        new Dictionary<string, object>
                        {
                            { "Result", true },
                            { "IsMatched", true },
                            { "JoinKey", joinKey },
                            { "ActualInputCount", bucket.Inputs.Count },
                            { "ExpectedInputCount", expectedInputCount }
                        }));
            }
        }

        private static NodeExecutionResult CreateWaitingResult(AndJoinBucket bucket)
        {
            return NodeExecutionResult.Success(
                "Waiting",
                new Dictionary<string, object>
                {
                    { "Result", false },
                    { "IsMatched", false },
                    { "JoinKey", bucket.JoinKey },
                    { "ActualInputCount", bucket.Inputs.Count },
                    { "ExpectedInputCount", bucket.ExpectedInputCount }
                });
        }
    }

    public sealed class ConditionNodeConfig
    {
        public ConditionNodeConfig()
        {
            Operator = "Equal";
        }

        public string LeftBinding { get; set; }

        public string Operator { get; set; }

        public object RightValue { get; set; }

        public string RightBinding { get; set; }
    }

    public sealed class ConditionNodeFactory : BaseNodeFactory<ConditionNodeConfig>
    {
        public const string TypeName = "condition.if";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return ConditionNodeDescriptor.Create(); }
        }

        protected override ConditionNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new ConditionNodeConfig
            {
                LeftBinding = GetStringSetting(definition, "LeftBinding", null),
                Operator = GetStringSetting(definition, "Operator", "Equal"),
                RightValue = GetSetting(definition, "RightValue", null),
                RightBinding = GetStringSetting(definition, "RightBinding", null)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, ConditionNodeConfig config)
        {
            return new ConditionNode(config);
        }
    }

    public sealed class ConditionNode : IFlowNode
    {
        private readonly ConditionNodeConfig _config;

        public ConditionNode(ConditionNodeConfig config)
        {
            _config = config ?? new ConditionNodeConfig();
        }

        public Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var leftBinding = ControlFlowNodeHelpers.ResolveString(context, "LeftBinding", _config.LeftBinding);
                if (string.IsNullOrWhiteSpace(leftBinding))
                {
                    return Task.FromResult(NodeExecutionResult.Failure("LeftBinding is required."));
                }

                var operatorName = ControlFlowNodeHelpers.ResolveString(context, "Operator", _config.Operator);
                if (string.IsNullOrWhiteSpace(operatorName))
                {
                    operatorName = "Equal";
                }

                var left = ControlFlowNodeHelpers.ResolveBindingExpression(context, leftBinding);
                var rightBinding = ControlFlowNodeHelpers.ResolveString(context, "RightBinding", _config.RightBinding);
                var right = string.IsNullOrWhiteSpace(rightBinding)
                    ? ControlFlowNodeHelpers.ResolveObject(context, "RightValue", _config.RightValue)
                    : ControlFlowNodeHelpers.ResolveBindingExpression(context, rightBinding);

                var isMatched = Evaluate(left, operatorName, right);
                return Task.FromResult(
                    NodeExecutionResult.Success(
                        isMatched ? "True" : "False",
                        new Dictionary<string, object>
                        {
                            { "Result", isMatched },
                            { "IsMatched", isMatched },
                            { "Left", left },
                            { "Right", right },
                            { "Operator", operatorName }
                        }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(NodeExecutionResult.Failure(ex.Message));
            }
        }

        private static bool Evaluate(object left, string operatorName, object right)
        {
            if (string.Equals(operatorName, "Equal", StringComparison.OrdinalIgnoreCase))
            {
                return ValuesEqual(left, right);
            }

            if (string.Equals(operatorName, "NotEqual", StringComparison.OrdinalIgnoreCase))
            {
                return !ValuesEqual(left, right);
            }

            if (string.Equals(operatorName, "GreaterThan", StringComparison.OrdinalIgnoreCase))
            {
                return Compare(left, right) > 0;
            }

            if (string.Equals(operatorName, "LessThan", StringComparison.OrdinalIgnoreCase))
            {
                return Compare(left, right) < 0;
            }

            if (string.Equals(operatorName, "Contains", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToString(left, CultureInfo.InvariantCulture)
                    .IndexOf(Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (string.Equals(operatorName, "IsNull", StringComparison.OrdinalIgnoreCase))
            {
                return left == null;
            }

            if (string.Equals(operatorName, "IsNotNull", StringComparison.OrdinalIgnoreCase))
            {
                return left != null;
            }

            throw new InvalidOperationException("Unsupported condition operator: " + operatorName);
        }

        private static bool ValuesEqual(object left, object right)
        {
            decimal leftDecimal;
            decimal rightDecimal;
            if (TryConvertDecimal(left, out leftDecimal) && TryConvertDecimal(right, out rightDecimal))
            {
                return leftDecimal == rightDecimal;
            }

            return string.Equals(
                Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);
        }

        private static int Compare(object left, object right)
        {
            decimal leftDecimal;
            decimal rightDecimal;
            if (TryConvertDecimal(left, out leftDecimal) && TryConvertDecimal(right, out rightDecimal))
            {
                return leftDecimal.CompareTo(rightDecimal);
            }

            return string.Compare(
                Convert.ToString(left, CultureInfo.InvariantCulture),
                Convert.ToString(right, CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryConvertDecimal(object value, out decimal result)
        {
            result = 0;
            if (value == null)
            {
                return false;
            }

            try
            {
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static class AndJoinNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = AndJoinNodeFactory.TypeName,
                DisplayName = "AND Join",
                Category = "Flow",
                Version = "1.0.0",
                Description = "Collects multiple inputs by JoinKey and continues when all expected inputs arrive.",
                InputPorts =
                {
                    CreatePort("In", "In", "Input", "Execution input.")
                },
                OutputPorts =
                {
                    CreatePort("Next", "Next", "Output", "All expected inputs have arrived."),
                    CreatePort("Error", "Error", "Output", "Join configuration or duplicate input error.")
                },
                Settings =
                {
                    CreateStringSetting("JoinKeyBinding", "Join Key Binding", null, true, "Expression used to resolve the join key, for example {{ token.PositionId }}."),
                    CreateIntSetting("ExpectedInputCount", "Expected Inputs", 2, true, "Number of inputs required for the join key."),
                    CreateIntSetting("TimeoutMs", "Timeout (ms)", 0, false, "Reserved timeout. Zero disables timeout handling."),
                    CreateStringSetting("DuplicatePolicy", "Duplicate Policy", "Ignore", true, "Ignore, Replace, or Error when the same token arrives twice.")
                },
                Outputs =
                {
                    CreateOutput("Result", "Result", "Boolean", "True when the join completes."),
                    CreateOutput("IsMatched", "Is Matched", "Boolean", "True when all inputs are matched."),
                    CreateOutput("JoinKey", "Join Key", "String", "Resolved join key."),
                    CreateOutput("ActualInputCount", "Actual Inputs", "Int32", "Number of inputs currently collected."),
                    CreateOutput("ExpectedInputCount", "Expected Inputs", "Int32", "Expected input count.")
                }
            };
        }

        private static NodePortDescriptor CreatePort(string name, string displayName, string direction, string description)
        {
            return new NodePortDescriptor
            {
                Name = name,
                DisplayName = displayName,
                Direction = direction,
                DataType = "Control",
                IsRequired = string.Equals(direction, "Input", StringComparison.OrdinalIgnoreCase),
                Description = description
            };
        }

        private static NodeSettingDescriptor CreateStringSetting(string name, string displayName, string defaultValue, bool isRequired, string description)
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

        private static NodeSettingDescriptor CreateIntSetting(string name, string displayName, int defaultValue, bool isRequired, string description)
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

        private static NodeOutputDescriptor CreateOutput(string name, string displayName, string dataType, string description)
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

    public static class ConditionNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = ConditionNodeFactory.TypeName,
                DisplayName = "Condition",
                Category = "Flow",
                Version = "1.0.0",
                Description = "Routes execution through True or False according to a configured comparison.",
                InputPorts =
                {
                    CreatePort("In", "In", "Input", "Execution input.")
                },
                OutputPorts =
                {
                    CreatePort("True", "True", "Output", "Condition matched."),
                    CreatePort("False", "False", "Output", "Condition did not match."),
                    CreatePort("Error", "Error", "Output", "Condition evaluation failed.")
                },
                Settings =
                {
                    CreateStringSetting("LeftBinding", "Left Binding", null, true, "Expression used to resolve the left value."),
                    CreateStringSetting("Operator", "Operator", "Equal", true, "Equal, NotEqual, GreaterThan, LessThan, Contains, IsNull, or IsNotNull."),
                    CreateObjectSetting("RightValue", "Right Value", null, false, "Constant right value."),
                    CreateStringSetting("RightBinding", "Right Binding", null, false, "Optional expression used to resolve the right value.")
                },
                Outputs =
                {
                    CreateOutput("Result", "Result", "Boolean", "Condition evaluation result."),
                    CreateOutput("IsMatched", "Is Matched", "Boolean", "Alias for Result."),
                    CreateOutput("Left", "Left", "Object", "Resolved left value."),
                    CreateOutput("Right", "Right", "Object", "Resolved right value."),
                    CreateOutput("Operator", "Operator", "String", "Operator used for evaluation.")
                }
            };
        }

        private static NodePortDescriptor CreatePort(string name, string displayName, string direction, string description)
        {
            return new NodePortDescriptor
            {
                Name = name,
                DisplayName = displayName,
                Direction = direction,
                DataType = "Control",
                IsRequired = string.Equals(direction, "Input", StringComparison.OrdinalIgnoreCase),
                Description = description
            };
        }

        private static NodeSettingDescriptor CreateStringSetting(string name, string displayName, string defaultValue, bool isRequired, string description)
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

        private static NodeSettingDescriptor CreateObjectSetting(string name, string displayName, object defaultValue, bool isRequired, string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "Object",
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        private static NodeOutputDescriptor CreateOutput(string name, string displayName, string dataType, string description)
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

    internal sealed class AndJoinBucket
    {
        public AndJoinBucket(string joinKey, int expectedInputCount, int timeoutMs)
        {
            JoinKey = joinKey;
            ExpectedInputCount = expectedInputCount;
            TimeoutMs = timeoutMs;
            CreatedAtUtc = DateTime.UtcNow;
            Inputs = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        }

        public string JoinKey { get; private set; }

        public int ExpectedInputCount { get; set; }

        public int TimeoutMs { get; set; }

        public DateTime CreatedAtUtc { get; private set; }

        public Dictionary<string, DateTime> Inputs { get; private set; }

        public bool IsExpired()
        {
            return TimeoutMs > 0 && DateTime.UtcNow - CreatedAtUtc > TimeSpan.FromMilliseconds(TimeoutMs);
        }
    }

    internal static class ControlFlowNodeHelpers
    {
        public static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = ResolveObject(context, name, defaultValue);
            return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
        {
            var value = ResolveObject(context, name, defaultValue);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static object ResolveObject(FlowExecutionContext context, string name, object defaultValue)
        {
            var value = context.GetInputValue(name);
            return value ?? defaultValue;
        }

        public static object ResolveBindingExpression(FlowExecutionContext context, string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return null;
            }

            var trimmed = expression.Trim();
            if (trimmed.StartsWith("{{", StringComparison.Ordinal) && trimmed.EndsWith("}}", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(2, trimmed.Length - 4).Trim();
            }

            if (trimmed.StartsWith("token.", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveTokenValue(context.Token, trimmed.Substring("token.".Length));
            }

            string sourceNodeId;
            string sourceOutputName;
            if (VariableBinding.TryParseVariablePath(expression, out sourceNodeId, out sourceOutputName))
            {
                return context.ResolveBinding(VariableBinding.ForExpression(expression));
            }

            return expression;
        }

        private static object ResolveTokenValue(FlowToken token, string name)
        {
            if (token == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (string.Equals(name, "TokenId", StringComparison.OrdinalIgnoreCase))
            {
                return token.TokenId;
            }

            if (string.Equals(name, "CreatedAtUtc", StringComparison.OrdinalIgnoreCase))
            {
                return token.CreatedAtUtc;
            }

            if (string.Equals(name, "ProductId", StringComparison.OrdinalIgnoreCase))
            {
                return token.ProductId;
            }

            if (string.Equals(name, "WorkpieceId", StringComparison.OrdinalIgnoreCase))
            {
                return token.WorkpieceId;
            }

            if (string.Equals(name, "PositionId", StringComparison.OrdinalIgnoreCase))
            {
                return token.PositionId;
            }

            if (string.Equals(name, "CaptureGroupId", StringComparison.OrdinalIgnoreCase))
            {
                return token.CaptureGroupId;
            }

            if (string.Equals(name, "ScanGroupId", StringComparison.OrdinalIgnoreCase))
            {
                return token.ScanGroupId;
            }

            if (string.Equals(name, "FrameId", StringComparison.OrdinalIgnoreCase))
            {
                return token.FrameId;
            }

            if (name.StartsWith("Metadata.", StringComparison.OrdinalIgnoreCase) && token.Metadata != null)
            {
                object value;
                return token.Metadata.TryGetValue(name.Substring("Metadata.".Length), out value) ? value : null;
            }

            if (name.StartsWith("Values.", StringComparison.OrdinalIgnoreCase) && token.Values != null)
            {
                object value;
                return token.Values.TryGetValue(name.Substring("Values.".Length), out value) ? value : null;
            }

            object tokenValue;
            return token.TryGet(name, out tokenValue) ? tokenValue : null;
        }
    }
}
