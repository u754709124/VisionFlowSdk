using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Services.Serialization
{
    internal static partial class FlowSerializationMapper
    {
        private static object ToSerializableNodeExecutionPolicy(NodeExecutionPolicy policy)
        {
            var effective = policy ?? new NodeExecutionPolicy();
            var retry = effective.RetryPolicy ?? new RetryPolicy();
            return new Dictionary<string, object>
            {
                { "TimeoutMs", effective.TimeoutMs },
                { "MaxConcurrentExecutions", effective.MaxConcurrentExecutions },
                { "RetryPolicy", new Dictionary<string, object>
                    {
                        { "Enabled", retry.Enabled },
                        { "MaxRetries", retry.MaxRetries },
                        { "RetryIntervalMs", retry.RetryIntervalMs }
                    }
                },
                { "FailureStrategy", FlowEnumConverter.ToWireValue(effective.FailureStrategy) },
                { "DefaultOutputs", NormalizeObject(effective.DefaultOutputs ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)) }
            };
        }

        private static NodeExecutionPolicy ToNodeExecutionPolicy(object value)
        {
            var dictionary = AsDictionary(value);
            var policy = new NodeExecutionPolicy
            {
                TimeoutMs = GetInt32(dictionary, "TimeoutMs", 0),
                MaxConcurrentExecutions = GetInt32(dictionary, "MaxConcurrentExecutions", 1)
            };

            object retryValue;
            if (TryGetValue(dictionary, "RetryPolicy", out retryValue) && retryValue != null)
            {
                policy.RetryPolicy = ToRetryPolicy(retryValue);
            }

            object failureStrategyValue;
            if (TryGetValue(dictionary, "FailureStrategy", out failureStrategyValue) && failureStrategyValue != null)
            {
                try
                {
                    policy.FailureStrategy = FlowEnumConverter.Parse<FailureStrategy>(failureStrategyValue);
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidOperationException("Node execution FailureStrategy is invalid.", ex);
                }
            }

            object defaultOutputsValue;
            if (TryGetValue(dictionary, "DefaultOutputs", out defaultOutputsValue) && defaultOutputsValue != null)
            {
                policy.DefaultOutputs = ToCaseInsensitiveObjectDictionary(defaultOutputsValue);
            }

            return policy;
        }

        private static RetryPolicy ToRetryPolicy(object value)
        {
            var dictionary = AsDictionary(value);
            return new RetryPolicy
            {
                Enabled = GetBoolean(dictionary, "Enabled", false),
                MaxRetries = GetInt32(dictionary, "MaxRetries", 3),
                RetryIntervalMs = GetInt32(dictionary, "RetryIntervalMs", 1000)
            };
        }

        private static Dictionary<string, object> ToCaseInsensitiveObjectDictionary(object value)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var dictionary = AsDictionaryOrNull(value);
            if (dictionary == null)
            {
                return result;
            }

            foreach (var item in dictionary)
            {
                result[item.Key] = NormalizeObject(item.Value);
            }

            return result;
        }
    }
}
