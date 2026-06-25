using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Web.Script.Serialization;

namespace Vision.Flow.Core
{
    public static class FlowDesignSerializer
    {
        public static string Serialize(FlowDesignDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            return CreateSerializer().Serialize(document);
        }

        public static FlowDesignDocument Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON content is required.", "json");
            }

            return FlowSerializationMapper.ToDesignDocument(CreateSerializer().DeserializeObject(json));
        }

        public static void Save(string path, FlowDesignDocument document)
        {
            File.WriteAllText(path, Serialize(document));
        }

        public static FlowDesignDocument Load(string path)
        {
            return Deserialize(File.ReadAllText(path));
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            };
        }
    }

    public static class RuntimeFlowSerializer
    {
        public static string Serialize(RuntimeFlowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            return CreateSerializer().Serialize(definition);
        }

        public static RuntimeFlowDefinition Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("JSON content is required.", "json");
            }

            return FlowSerializationMapper.ToRuntimeFlowDefinition(CreateSerializer().DeserializeObject(json));
        }

        public static void Save(string path, RuntimeFlowDefinition definition)
        {
            File.WriteAllText(path, Serialize(definition));
        }

        public static RuntimeFlowDefinition Load(string path)
        {
            return Deserialize(File.ReadAllText(path));
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            };
        }
    }

    internal static class FlowSerializationMapper
    {
        public static FlowDesignDocument ToDesignDocument(object value)
        {
            var dictionary = AsDictionary(value);
            var document = new FlowDesignDocument
            {
                FlowId = GetString(dictionary, "FlowId"),
                FlowName = GetString(dictionary, "FlowName"),
                SchemaVersion = GetInt32(dictionary, "SchemaVersion", 1)
            };

            object runtimeValue;
            if (TryGetValue(dictionary, "Runtime", out runtimeValue))
            {
                document.Runtime = ToRuntimeFlowDefinition(runtimeValue);
            }

            object viewValue;
            if (TryGetValue(dictionary, "View", out viewValue))
            {
                document.View = ToFlowViewState(viewValue);
            }

            return document;
        }

        public static RuntimeFlowDefinition ToRuntimeFlowDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            var definition = new RuntimeFlowDefinition
            {
                FlowId = GetString(dictionary, "FlowId"),
                FlowName = GetString(dictionary, "FlowName"),
                SchemaVersion = GetInt32(dictionary, "SchemaVersion", 1),
                Version = GetString(dictionary, "Version")
            };

            object settingsValue;
            if (TryGetValue(dictionary, "Settings", out settingsValue))
            {
                definition.Settings = ToObjectDictionary(settingsValue);
            }

            object nodesValue;
            if (TryGetValue(dictionary, "Nodes", out nodesValue))
            {
                foreach (var node in AsEnumerable(nodesValue))
                {
                    definition.Nodes.Add(ToNodeDefinition(node));
                }
            }

            object edgesValue;
            if (TryGetValue(dictionary, "Edges", out edgesValue))
            {
                foreach (var edge in AsEnumerable(edgesValue))
                {
                    definition.Edges.Add(ToEdgeDefinition(edge));
                }
            }

            object entriesValue;
            if (TryGetValue(dictionary, "Entries", out entriesValue))
            {
                foreach (var entry in AsEnumerable(entriesValue))
                {
                    definition.Entries.Add(ToEntryDefinition(entry));
                }
            }

            return definition;
        }

        private static FlowViewState ToFlowViewState(object value)
        {
            var dictionary = AsDictionary(value);
            var view = new FlowViewState
            {
                Zoom = GetDouble(dictionary, "Zoom", 1.0),
                OffsetX = GetDouble(dictionary, "OffsetX", 0),
                OffsetY = GetDouble(dictionary, "OffsetY", 0),
                CanvasWidth = GetDouble(dictionary, "CanvasWidth", FlowViewState.DefaultCanvasWidth),
                CanvasHeight = GetDouble(dictionary, "CanvasHeight", FlowViewState.DefaultCanvasHeight)
            };

            object nodesValue;
            if (TryGetValue(dictionary, "Nodes", out nodesValue))
            {
                foreach (var item in ToObjectDictionary(nodesValue))
                {
                    view.Nodes[item.Key] = ToNodeViewState(item.Value);
                }
            }

            return view;
        }

        private static NodeViewState ToNodeViewState(object value)
        {
            var dictionary = AsDictionary(value);
            return new NodeViewState
            {
                X = GetDouble(dictionary, "X", 0),
                Y = GetDouble(dictionary, "Y", 0),
                IsCollapsed = GetBoolean(dictionary, "IsCollapsed", false)
            };
        }

        private static NodeDefinition ToNodeDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            var node = new NodeDefinition
            {
                Id = GetString(dictionary, "Id"),
                Type = GetString(dictionary, "Type"),
                Name = GetString(dictionary, "Name"),
                Version = GetString(dictionary, "Version")
            };

            object settingsValue;
            if (TryGetValue(dictionary, "Settings", out settingsValue))
            {
                node.Settings = ToObjectDictionary(settingsValue);
            }

            object inputBindingsValue;
            if (TryGetValue(dictionary, "InputBindings", out inputBindingsValue))
            {
                node.InputBindings = ToBindingDictionary(inputBindingsValue);
            }

            return node;
        }

        private static EdgeDefinition ToEdgeDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            return new EdgeDefinition
            {
                FromNodeId = GetString(dictionary, "FromNodeId"),
                FromPort = GetString(dictionary, "FromPort"),
                ToNodeId = GetString(dictionary, "ToNodeId"),
                ToPort = GetString(dictionary, "ToPort")
            };
        }

        private static FlowEntryDefinition ToEntryDefinition(object value)
        {
            var dictionary = AsDictionary(value);
            return new FlowEntryDefinition
            {
                EntryName = GetString(dictionary, "EntryName"),
                TargetNodeId = GetString(dictionary, "TargetNodeId")
            };
        }

        private static Dictionary<string, VariableBinding> ToBindingDictionary(object value)
        {
            var result = new Dictionary<string, VariableBinding>(StringComparer.Ordinal);
            foreach (var item in ToObjectDictionary(value))
            {
                var binding = item.Value as VariableBinding;
                if (binding != null)
                {
                    result[item.Key] = binding;
                    continue;
                }

                var text = item.Value as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result[item.Key] = VariableBinding.ForExpression(text);
                    continue;
                }

                var dictionary = item.Value as IDictionary<string, object>;
                if (dictionary != null)
                {
                    result[item.Key] = ToVariableBinding(dictionary);
                }
            }

            return result;
        }

        private static VariableBinding ToVariableBinding(IDictionary<string, object> dictionary)
        {
            var binding = new VariableBinding
            {
                Expression = GetString(dictionary, "Expression"),
                SourceNodeId = GetString(dictionary, "SourceNodeId"),
                SourceOutputName = GetString(dictionary, "SourceOutputName"),
                ConstantValue = GetObject(dictionary, "ConstantValue"),
                ValueType = GetString(dictionary, "ValueType"),
                IsConstant = GetBoolean(dictionary, "IsConstant", false)
            };

            if (!binding.IsConstant &&
                string.IsNullOrWhiteSpace(binding.SourceNodeId) &&
                string.IsNullOrWhiteSpace(binding.SourceOutputName) &&
                !string.IsNullOrWhiteSpace(binding.Expression))
            {
                return VariableBinding.ForExpression(binding.Expression);
            }

            return binding;
        }

        private static Dictionary<string, object> ToObjectDictionary(object value)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
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

        private static object NormalizeObject(object value)
        {
            var dictionary = AsDictionaryOrNull(value);
            if (dictionary != null)
            {
                return ToObjectDictionary(dictionary);
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(NormalizeObject(item));
                }

                return list;
            }

            return value;
        }

        private static IDictionary<string, object> AsDictionary(object value)
        {
            var dictionary = AsDictionaryOrNull(value);
            if (dictionary == null)
            {
                throw new InvalidOperationException("Expected a JSON object.");
            }

            return dictionary;
        }

        private static IDictionary<string, object> AsDictionaryOrNull(object value)
        {
            var typed = value as IDictionary<string, object>;
            if (typed != null)
            {
                return typed;
            }

            var dictionary = value as IDictionary;
            if (dictionary == null)
            {
                return null;
            }

            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in dictionary)
            {
                result[Convert.ToString(item.Key, CultureInfo.InvariantCulture)] = item.Value;
            }

            return result;
        }

        private static IEnumerable<object> AsEnumerable(object value)
        {
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                yield break;
            }

            foreach (var item in enumerable)
            {
                yield return item;
            }
        }

        private static bool TryGetValue(IDictionary<string, object> dictionary, string key, out object value)
        {
            value = null;
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (var item in dictionary)
            {
                if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            return false;
        }

        private static object GetObject(IDictionary<string, object> dictionary, string key)
        {
            object value;
            return TryGetValue(dictionary, key, out value) ? NormalizeObject(value) : null;
        }

        private static string GetString(IDictionary<string, object> dictionary, string key)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;
        }

        private static int GetInt32(IDictionary<string, object> dictionary, string key, int defaultValue)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
                : defaultValue;
        }

        private static double GetDouble(IDictionary<string, object> dictionary, string key, double defaultValue)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : defaultValue;
        }

        private static bool GetBoolean(IDictionary<string, object> dictionary, string key, bool defaultValue)
        {
            object value;
            return TryGetValue(dictionary, key, out value) && value != null
                ? Convert.ToBoolean(value, CultureInfo.InvariantCulture)
                : defaultValue;
        }
    }
}
