using System;
using System.IO;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Domain.Flows;

namespace Vision.Flow.Core.Services.Serialization
{
    public static class FlowDesignSerializer
    {
        public static string Serialize(FlowDesignDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            return CreateSerializer().Serialize(FlowSerializationMapper.ToSerializableDesignDocument(document));
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
}
