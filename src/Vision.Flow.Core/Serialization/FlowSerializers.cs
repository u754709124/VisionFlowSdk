using System;
using System.IO;
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

            return CreateSerializer().Deserialize<FlowDesignDocument>(json);
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

            return CreateSerializer().Deserialize<RuntimeFlowDefinition>(json);
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
}
