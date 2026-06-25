using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // Shared descriptor builders keep adapter node ports and queue settings uniform.
    internal static class AdapterNodeDescriptors
    {
        public static NodePortDescriptor ControlIn()
        {
            return new NodePortDescriptor
            {
                Name = "In",
                DisplayName = "In",
                Direction = "Input",
                DataType = "Control",
                IsRequired = true,
                Description = "Execution input."
            };
        }

        public static NodePortDescriptor NextOut()
        {
            return new NodePortDescriptor
            {
                Name = "Next",
                DisplayName = "Next",
                Direction = "Output",
                DataType = "Control",
                Description = "Continues after successful execution."
            };
        }

        public static NodePortDescriptor ErrorOut(string description)
        {
            return new NodePortDescriptor
            {
                Name = "Error",
                DisplayName = "Error",
                Direction = "Output",
                DataType = "Control",
                Description = description
            };
        }

        public static NodePortDescriptor TimeoutOut(string description)
        {
            return new NodePortDescriptor
            {
                Name = "Timeout",
                DisplayName = "Timeout",
                Direction = "Output",
                DataType = "Control",
                Description = description
            };
        }

        public static NodeSettingDescriptor QueueUseSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = "UseQueue",
                DisplayName = "Use Queue",
                DataType = "Boolean",
                DefaultValue = false,
                IsRequired = false,
                Description = "When true, runs adapter work through a bounded runtime queue."
            };
        }

        public static NodeSettingDescriptor QueueNameSetting(string defaultValue)
        {
            return new NodeSettingDescriptor
            {
                Name = "QueueName",
                DisplayName = "Queue Name",
                DataType = "String",
                DefaultValue = defaultValue,
                IsRequired = false,
                Description = "Runtime queue name used when UseQueue is enabled."
            };
        }

        public static NodeSettingDescriptor QueueCapacitySetting()
        {
            return new NodeSettingDescriptor
            {
                Name = "QueueCapacity",
                DisplayName = "Queue Capacity",
                DataType = "Int32",
                DefaultValue = 16,
                IsRequired = false,
                Description = "Maximum number of running and waiting items in the queue."
            };
        }

        public static NodeSettingDescriptor QueueMaxDegreeSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = "QueueMaxDegreeOfParallelism",
                DisplayName = "Queue Parallelism",
                DataType = "Int32",
                DefaultValue = 1,
                IsRequired = false,
                Description = "Maximum concurrent adapter calls for this queue."
            };
        }

        public static NodeSettingDescriptor QueueFullModeSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = "QueueFullMode",
                DisplayName = "Queue Full Mode",
                DataType = "String",
                DefaultValue = "Wait",
                IsRequired = false,
                Description = "Queue behavior when full. Supported values: Wait, Reject, Drop, StopFlow, NotifyOnly."
            };
        }

        public static NodeSettingDescriptor QueueWaitForCompletionSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = "WaitForCompletion",
                DisplayName = "Wait For Completion",
                DataType = "Boolean",
                DefaultValue = true,
                IsRequired = false,
                Description = "When false, returns after queued work is accepted and lets queue events report background completion."
            };
        }
    }
}
