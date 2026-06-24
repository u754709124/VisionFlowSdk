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
            registry.Register(new CameraSetParameterNodeFactory());
            registry.Register(new CameraSoftTriggerNodeFactory());
            registry.Register(new CameraImageCallbackNodeFactory());
            registry.Register(new LightControlNodeFactory());
            registry.Register(new RecipeRunNodeFactory());
            registry.Register(new ImageSaveNodeFactory());
            registry.Register(new DatabaseSaveNodeFactory());
        }
    }
}
