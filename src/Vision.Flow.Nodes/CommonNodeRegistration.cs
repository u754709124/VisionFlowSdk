using System;
using Vision.Flow.Core;

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
        }
    }
}
