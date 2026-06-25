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
    // 光源控制节点将流程设置转换为适配器通道命令。
    public sealed class LightChannelControlConfig
    {
        public LightChannelControlConfig()
        {
            IsEnabled = true;
        }

        public string ChannelName { get; set; }

        public bool IsEnabled { get; set; }

        public double Intensity { get; set; }

        public int DurationMs { get; set; }
    }

    public sealed class LightControlNodeConfig
    {
        public LightControlNodeConfig()
        {
            Channels = new List<LightChannelControlConfig>();
        }

        public string LightId { get; set; }

        public IList<LightChannelControlConfig> Channels { get; set; }

        public int StableDelayMs { get; set; }
    }

    public sealed class LightControlNodeFactory : BaseNodeFactory<LightControlNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.LightControl;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return LightControlNodeDescriptor.Create(); }
        }

        protected override LightControlNodeConfig CreateConfig(NodeDefinition definition)
        {
            var config = new LightControlNodeConfig
            {
                LightId = GetStringSetting(definition, "LightId", null),
                StableDelayMs = GetInt32Setting(definition, "StableDelayMs", 0)
            };

            AdapterNodeHelpers.AddLightChannels(config.Channels, GetSetting(definition, "Channels", null));

            var channelName = GetStringSetting(definition, "ChannelName", null);
            if (!string.IsNullOrWhiteSpace(channelName))
            {
                config.Channels.Add(new LightChannelControlConfig
                {
                    ChannelName = channelName,
                    IsEnabled = Convert.ToBoolean(GetSetting(definition, "IsEnabled", true), CultureInfo.InvariantCulture),
                    Intensity = Convert.ToDouble(GetSetting(definition, "Intensity", 0.0), CultureInfo.InvariantCulture),
                    DurationMs = GetInt32Setting(definition, "DurationMs", 0)
                });
            }

            return config;
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, LightControlNodeConfig config)
        {
            return new LightControlNode(config);
        }
    }

    public sealed class LightControlNode : IFlowNode
    {
        private readonly LightControlNodeConfig _config;

        public LightControlNode(LightControlNodeConfig config)
        {
            _config = config ?? new LightControlNodeConfig();
            if (_config.Channels == null)
            {
                _config.Channels = new List<LightChannelControlConfig>();
            }
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lightId = AdapterNodeHelpers.ResolveString(context, "LightId", _config.LightId);
            if (string.IsNullOrWhiteSpace(lightId))
            {
                return NodeExecutionResult.Failure("LightId is required.");
            }

            var stableDelayMs = AdapterNodeHelpers.ResolveInt32(context, "StableDelayMs", _config.StableDelayMs);
            if (stableDelayMs < 0)
            {
                return NodeExecutionResult.Failure("StableDelayMs must be greater than or equal to zero.");
            }

            var channels = ResolveChannels(context);
            if (channels.Count == 0)
            {
                return NodeExecutionResult.Failure("At least one light channel is required.");
            }

            var light = context.Devices.GetLight(lightId);
            var applied = new List<LightChannelSetting>();

            foreach (var channel in channels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (channel == null || string.IsNullOrWhiteSpace(channel.ChannelName))
                {
                    return NodeExecutionResult.Failure("Light channel name is required.");
                }

                var setting = new LightChannelSetting
                {
                    LightId = lightId,
                    ChannelName = channel.ChannelName,
                    IsEnabled = channel.IsEnabled,
                    Intensity = channel.Intensity,
                    DurationMs = channel.DurationMs
                };
                await light.SetAsync(setting, cancellationToken).ConfigureAwait(false);
                applied.Add(setting);
            }

            if (stableDelayMs > 0)
            {
                await Task.Delay(stableDelayMs, cancellationToken).ConfigureAwait(false);
            }

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "LightId", lightId },
                    { "ChannelCount", applied.Count },
                    { "StableDelayMs", stableDelayMs },
                    { "Channels", applied }
                });
        }

        private IList<LightChannelControlConfig> ResolveChannels(FlowExecutionContext context)
        {
            if (context.Node.InputBindings != null && context.Node.InputBindings.ContainsKey("Channels"))
            {
                var channels = new List<LightChannelControlConfig>();
                AdapterNodeHelpers.AddLightChannels(channels, context.GetInputValue("Channels"));
                return channels;
            }

            return _config.Channels;
        }
    }

    public static class LightControlNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = LightControlNodeFactory.TypeName,
                DisplayName = "Light Control",
                Category = "Device",
                Version = "1.0.0",
                Description = "Sets one or more light channels through a light adapter.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "LightId",
                        DisplayName = "Light",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered light adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "Channels",
                        DisplayName = "Channels",
                        DataType = "Object",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Channel settings with ChannelName, IsEnabled, Intensity, and DurationMs."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "StableDelayMs",
                        DisplayName = "Stable Delay (ms)",
                        DataType = "Int32",
                        DefaultValue = 0,
                        IsRequired = false,
                        Description = "Delay after applying light channels."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "LightId",
                        DisplayName = "Light",
                        DataType = "String",
                        Description = "The resolved light id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ChannelCount",
                        DisplayName = "Channel Count",
                        DataType = "Int32",
                        Description = "Number of applied channels."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "StableDelayMs",
                        DisplayName = "Stable Delay",
                        DataType = "Int32",
                        Description = "The resolved stable delay."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Channels",
                        DisplayName = "Channels",
                        DataType = "Object",
                        Description = "Applied channel settings."
                    }
                }
            };
        }
    }
}
