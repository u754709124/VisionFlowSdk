using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Services.Validation;

namespace Vision.Flow.Core.Services.Publishing
{
    public sealed class FlowPublishService
    {
        private readonly FlowValidator _validator;

        public FlowPublishService(NodeRegistry nodeRegistry)
            : this(new FlowValidator(nodeRegistry))
        {
        }

        public FlowPublishService(FlowValidator validator)
        {
            if (validator == null)
            {
                throw new ArgumentNullException("validator");
            }

            _validator = validator;
        }

        public FlowPublishResult Publish(FlowDesignDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            FlowSchema.EnsureSupported(document.SchemaVersion);
            if (document.Runtime != null)
            {
                FlowSchema.EnsureSupported(document.Runtime.SchemaVersion);
            }

            var runtime = CreateRuntime(document);
            var validation = _validator.Validate(runtime);
            return new FlowPublishResult(runtime, validation);
        }

        public FlowValidationResult TryPublish(FlowDesignDocument document, out RuntimeFlowDefinition runtime)
        {
            var result = Publish(document);
            runtime = result.IsSuccess ? result.Runtime : null;
            return result.Validation;
        }

        public RuntimeFlowDefinition PublishRuntime(FlowDesignDocument document)
        {
            var result = Publish(document);
            if (result.IsSuccess)
            {
                return result.Runtime;
            }

            var firstError = result.Validation.Errors.FirstOrDefault();
            var message = firstError == null
                ? "Flow could not be published."
                : "Flow could not be published: " + firstError.Code + " - " + firstError.Message;
            throw new InvalidOperationException(message);
        }

        private static RuntimeFlowDefinition CreateRuntime(FlowDesignDocument document)
        {
            FlowSchema.EnsureSupported(document.SchemaVersion);
            var source = document.Runtime ?? new RuntimeFlowDefinition();
            FlowSchema.EnsureSupported(source.SchemaVersion);
            var runtime = new RuntimeFlowDefinition
            {
                FlowId = string.IsNullOrWhiteSpace(source.FlowId) ? document.FlowId : source.FlowId,
                FlowName = string.IsNullOrWhiteSpace(source.FlowName) ? document.FlowName : source.FlowName,
                SchemaVersion = source.SchemaVersion == 0 ? document.SchemaVersion : source.SchemaVersion,
                Version = source.Version,
                Settings = CloneObjectDictionary(source.Settings),
                Nodes = new List<NodeDefinition>(),
                Edges = new List<EdgeDefinition>(),
                Entries = new List<FlowEntryDefinition>()
            };

            var nodes = source.Nodes ?? new List<NodeDefinition>();
            for (var index = 0; index < nodes.Count; index++)
            {
                runtime.Nodes.Add(CloneNode(nodes[index]));
            }

            var edges = source.Edges ?? new List<EdgeDefinition>();
            for (var index = 0; index < edges.Count; index++)
            {
                runtime.Edges.Add(CloneEdge(edges[index]));
            }

            var entries = source.Entries ?? new List<FlowEntryDefinition>();
            for (var index = 0; index < entries.Count; index++)
            {
                runtime.Entries.Add(CloneEntry(entries[index]));
            }

            return runtime;
        }

        private static NodeDefinition CloneNode(NodeDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            return new NodeDefinition
            {
                Id = source.Id,
                Type = source.Type,
                Name = source.Name,
                Version = source.Version,
                Settings = CloneSettingDictionary(source.Settings)
            };
        }

        private static EdgeDefinition CloneEdge(EdgeDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            return new EdgeDefinition
            {
                FromNodeId = source.FromNodeId,
                FromPort = source.FromPort,
                ToNodeId = source.ToNodeId,
                ToPort = source.ToPort
            };
        }

        private static FlowEntryDefinition CloneEntry(FlowEntryDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            return new FlowEntryDefinition
            {
                EntryName = source.EntryName,
                TargetNodeId = source.TargetNodeId,
                SourceNodeId = source.SourceNodeId,
                TriggerKind = source.TriggerKind,
                Inputs = CloneTriggerInputs(source.Inputs),
                ExecutionPolicy = CloneTriggerExecutionPolicy(source.ExecutionPolicy)
            };
        }

        private static List<TriggerInputDescriptor> CloneTriggerInputs(IList<TriggerInputDescriptor> source)
        {
            var result = new List<TriggerInputDescriptor>();
            if (source == null)
            {
                return result;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var input = source[index];
                result.Add(input == null
                    ? null
                    : new TriggerInputDescriptor
                    {
                        Name = input.Name,
                        DisplayName = input.DisplayName,
                        DataType = input.DataType,
                        IsRequired = input.IsRequired,
                        DefaultValue = CloneObjectValue(input.DefaultValue),
                        Description = input.Description
                    });
            }

            return result;
        }

        private static TriggerExecutionPolicy CloneTriggerExecutionPolicy(TriggerExecutionPolicy source)
        {
            var effective = source ?? new TriggerExecutionPolicy();
            return new TriggerExecutionPolicy
            {
                MaxConcurrentRuns = effective.MaxConcurrentRuns,
                QueueCapacity = effective.QueueCapacity,
                QueueFullBehavior = effective.QueueFullBehavior
            };
        }

        private static Dictionary<string, object> CloneObjectDictionary(IDictionary<string, object> source)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (source == null)
            {
                return result;
            }

            foreach (var item in source)
            {
                result[item.Key] = CloneObjectValue(item.Value);
            }

            return result;
        }

        private static Dictionary<string, NodeSettingValue> CloneSettingDictionary(IDictionary<string, NodeSettingValue> source)
        {
            var result = new Dictionary<string, NodeSettingValue>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
            {
                return result;
            }

            foreach (var item in source)
            {
                result[item.Key] = CloneSettingValue(item.Value);
            }

            return result;
        }

        private static object CloneObjectValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value.GetType().IsEnum)
            {
                return FlowEnumConverter.NormalizeValue(value);
            }

            if (value is string)
            {
                return value;
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (DictionaryEntry item in dictionary)
                {
                    var key = Convert.ToString(item.Key, CultureInfo.InvariantCulture);
                    result[key] = CloneObjectValue(item.Value);
                }

                return result;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                var result = new List<object>();
                foreach (var item in enumerable)
                {
                    result.Add(CloneObjectValue(item));
                }

                return result;
            }

            return value;
        }

        private static NodeSettingValue CloneSettingValue(NodeSettingValue source)
        {
            if (source == null)
            {
                return null;
            }

            return new NodeSettingValue
            {
                Mode = source.Mode,
                ConstantValue = CloneObjectValue(source.ConstantValue),
                Selector = CloneSelector(source.Selector)
            };
        }

        private static VariableSelector CloneSelector(VariableSelector source)
        {
            if (source == null)
            {
                return null;
            }

            return new VariableSelector
            {
                Scope = source.Scope,
                Path = source.Path == null ? new List<string>() : new List<string>(source.Path)
            };
        }
    }
}
