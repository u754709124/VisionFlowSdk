using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Domain.Flows;
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
            var source = document.Runtime ?? new RuntimeFlowDefinition();
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
                Settings = CloneObjectDictionary(source.Settings),
                InputBindings = CloneBindingDictionary(source.InputBindings)
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
                TargetNodeId = source.TargetNodeId
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

        private static Dictionary<string, VariableBinding> CloneBindingDictionary(IDictionary<string, VariableBinding> source)
        {
            var result = new Dictionary<string, VariableBinding>(StringComparer.Ordinal);
            if (source == null)
            {
                return result;
            }

            foreach (var item in source)
            {
                result[item.Key] = CloneBinding(item.Value);
            }

            return result;
        }

        private static object CloneObjectValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            var binding = value as VariableBinding;
            if (binding != null)
            {
                return CloneBinding(binding);
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

        private static VariableBinding CloneBinding(VariableBinding source)
        {
            if (source == null)
            {
                return null;
            }

            return new VariableBinding
            {
                Expression = source.Expression,
                SourceNodeId = source.SourceNodeId,
                SourceOutputName = source.SourceOutputName,
                ConstantValue = CloneObjectValue(source.ConstantValue),
                ValueType = source.ValueType,
                IsConstant = source.IsConstant
            };
        }
    }
}
