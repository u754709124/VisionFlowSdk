using System;
using System.IO;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Serialization
{
    public static class RuntimeFlowSerializer
    {
        public static string Serialize(RuntimeFlowDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            FlowSchema.EnsureSupported(definition.SchemaVersion);

            return CreateSerializer().Serialize(FlowSerializationMapper.ToSerializableRuntimeFlowDefinition(definition));
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
}
