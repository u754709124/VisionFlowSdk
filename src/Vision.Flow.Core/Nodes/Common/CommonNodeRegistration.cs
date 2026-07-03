using System;
using Vision.Flow.Core.Contracts.Nodes;

namespace Vision.Flow.Nodes
{
    public static class CommonNodeRegistration
    {
        public static void RegisterAll(NodeRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException("registry");
            }

            registry.Register(new LogNodeFactory());
            registry.Register(new DelayNodeFactory());
            registry.Register(new SplitNodeFactory());
            registry.Register(new VariableSetNodeFactory());
            registry.Register(new AndJoinNodeFactory());
            registry.Register(new ConditionNodeFactory());
        }
    }
}
