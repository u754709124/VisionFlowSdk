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
    // 共享 Descriptor 构建器保持适配器节点端口和队列设置一致。
    internal static class AdapterNodeDescriptors
    {
        public static NodePortDescriptor ControlIn()
        {
            return new NodePortDescriptor
            {
                Name = FlowPortNames.In,
                DisplayName = FlowPortNames.In,
                Direction = FlowPortDirections.Input,
                DataType = FlowDataTypes.Control,
                IsRequired = true,
                Description = "Execution input."
            };
        }

        public static NodePortDescriptor NextOut()
        {
            return new NodePortDescriptor
            {
                Name = FlowPortNames.Next,
                DisplayName = FlowPortNames.Next,
                Direction = FlowPortDirections.Output,
                DataType = FlowDataTypes.Control,
                Description = "Continues after successful execution."
            };
        }

        public static NodePortDescriptor ErrorOut(string description)
        {
            return new NodePortDescriptor
            {
                Name = FlowPortNames.Error,
                DisplayName = FlowPortNames.Error,
                Direction = FlowPortDirections.Output,
                DataType = FlowDataTypes.Control,
                Description = description
            };
        }

        public static NodePortDescriptor TimeoutOut(string description)
        {
            return new NodePortDescriptor
            {
                Name = FlowPortNames.Timeout,
                DisplayName = FlowPortNames.Timeout,
                Direction = FlowPortDirections.Output,
                DataType = FlowDataTypes.Control,
                Description = description
            };
        }

        public static NodeSettingDescriptor QueueUseSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = FlowSettingNames.UseQueue,
                DisplayName = "Use Queue",
                DataType = FlowDataTypes.Boolean,
                DefaultValue = false,
                IsRequired = false,
                Description = "When true, runs adapter work through a bounded runtime queue."
            };
        }

        public static NodeSettingDescriptor QueueNameSetting(string defaultValue)
        {
            return new NodeSettingDescriptor
            {
                Name = FlowSettingNames.QueueName,
                DisplayName = "Queue Name",
                DataType = FlowDataTypes.String,
                DefaultValue = defaultValue,
                IsRequired = false,
                Description = "Runtime queue name used when UseQueue is enabled."
            };
        }

        public static NodeSettingDescriptor QueueCapacitySetting()
        {
            return new NodeSettingDescriptor
            {
                Name = FlowSettingNames.QueueCapacity,
                DisplayName = "Queue Capacity",
                DataType = FlowDataTypes.Int32,
                DefaultValue = 16,
                IsRequired = false,
                Description = "Maximum number of running and waiting items in the queue."
            };
        }

        public static NodeSettingDescriptor QueueMaxDegreeSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = FlowSettingNames.QueueMaxDegreeOfParallelism,
                DisplayName = "Queue Parallelism",
                DataType = FlowDataTypes.Int32,
                DefaultValue = 1,
                IsRequired = false,
                Description = "Maximum concurrent adapter calls for this queue."
            };
        }

        public static NodeSettingDescriptor QueueFullModeSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = FlowSettingNames.QueueFullMode,
                DisplayName = "Queue Full Mode",
                DataType = FlowDataTypes.String,
                DefaultValue = FlowQueueFullModeNames.Wait,
                IsRequired = false,
                Description = "Queue behavior when full. Supported values: Wait, Reject, Drop, StopFlow, NotifyOnly."
            };
        }

        public static NodeSettingDescriptor QueueWaitForCompletionSetting()
        {
            return new NodeSettingDescriptor
            {
                Name = FlowSettingNames.WaitForCompletion,
                DisplayName = "Wait For Completion",
                DataType = FlowDataTypes.Boolean,
                DefaultValue = true,
                IsRequired = false,
                Description = "When false, returns after queued work is accepted and lets queue events report background completion."
            };
        }
    }
}
