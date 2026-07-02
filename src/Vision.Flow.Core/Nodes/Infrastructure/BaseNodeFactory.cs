using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Vision.Flow.Core.Constants;
using Vision.Flow.Core.Definitions;
using Vision.Flow.Core.Descriptors;
using Vision.Flow.Core.Devices;
using Vision.Flow.Core.Publishing;
using Vision.Flow.Core.Registry;
using Vision.Flow.Core.Runtime;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Serialization;
using Vision.Flow.Core.Validation;

namespace Vision.Flow.Nodes
{
    public abstract class BaseNodeFactory<TConfig> : INodeFactory
        where TConfig : class, new()
    {
        public abstract string NodeType { get; }

        public abstract NodeDescriptor Descriptor { get; }

        public IFlowNode Create(NodeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            return CreateNode(definition, CreateConfig(definition));
        }

        protected virtual TConfig CreateConfig(NodeDefinition definition)
        {
            return new TConfig();
        }

        protected abstract IFlowNode CreateNode(NodeDefinition definition, TConfig config);

        protected static object GetSetting(NodeDefinition definition, string name, object defaultValue)
        {
            object value;
            if (definition.Settings != null && definition.Settings.TryGetValue(name, out value))
            {
                return value;
            }

            return defaultValue;
        }

        protected static string GetStringSetting(NodeDefinition definition, string name, string defaultValue)
        {
            var value = GetSetting(definition, name, defaultValue);
            return value == null ? null : Convert.ToString(value);
        }

        protected static int GetInt32Setting(NodeDefinition definition, string name, int defaultValue)
        {
            var value = GetSetting(definition, name, defaultValue);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
