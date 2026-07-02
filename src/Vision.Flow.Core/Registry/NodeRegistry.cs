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

namespace Vision.Flow.Core.Registry
{
    public sealed class NodeRegistry
    {
        private readonly Dictionary<string, INodeFactory> _factories;

        public NodeRegistry()
        {
            _factories = new Dictionary<string, INodeFactory>(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<NodeDescriptor> Descriptors
        {
            get
            {
                foreach (var factory in _factories.Values)
                {
                    if (factory.Descriptor != null)
                    {
                        yield return factory.Descriptor;
                    }
                }
            }
        }

        public void Register(INodeFactory factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }

            if (string.IsNullOrWhiteSpace(factory.NodeType))
            {
                throw new ArgumentException("Factory node type is required.", "factory");
            }

            _factories[factory.NodeType] = factory;
        }

        public bool TryGetFactory(string nodeType, out INodeFactory factory)
        {
            factory = null;
            if (string.IsNullOrWhiteSpace(nodeType))
            {
                return false;
            }

            return _factories.TryGetValue(nodeType, out factory);
        }

        public INodeFactory GetFactory(string nodeType)
        {
            INodeFactory factory;
            if (!TryGetFactory(nodeType, out factory))
            {
                throw new KeyNotFoundException("Node factory was not registered: " + nodeType);
            }

            return factory;
        }

        public IFlowNode CreateNode(NodeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            return GetFactory(definition.Type).Create(definition);
        }
    }
}
