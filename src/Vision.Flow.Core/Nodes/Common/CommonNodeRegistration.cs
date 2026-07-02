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
