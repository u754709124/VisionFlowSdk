using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Contracts.Nodes
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

        /// <summary>
        /// 解析指定节点实例当前生效的描述符。
        /// </summary>
        /// <remarks>
        /// 动态工厂通过 <see cref="IInstanceNodeDescriptorProvider"/> 返回实例描述符；
        /// 未实现扩展契约的工厂继续返回静态 Descriptor，保持现有节点兼容。
        /// </remarks>
        public NodeDescriptor ResolveDescriptor(NodeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            var factory = GetFactory(definition.Type);
            var provider = factory as IInstanceNodeDescriptorProvider;
            return provider == null
                ? factory.Descriptor
                : provider.GetDescriptor(definition);
        }

        /// <summary>
        /// 使用完整流程定义解析节点描述符；流程感知扩展优先于现有实例扩展。
        /// </summary>
        public NodeDescriptor ResolveDescriptor(
            RuntimeFlowDefinition flow,
            NodeDefinition definition)
        {
            if (flow == null)
                throw new ArgumentNullException("flow");
            if (definition == null)
                throw new ArgumentNullException("definition");

            var factory = GetFactory(definition.Type);
            var flowProvider = factory as IFlowDefinitionNodeDescriptorProvider;
            if (flowProvider != null)
                return flowProvider.GetDescriptor(flow, definition);

            var instanceProvider = factory as IInstanceNodeDescriptorProvider;
            return instanceProvider == null
                ? factory.Descriptor
                : instanceProvider.GetDescriptor(definition);
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
