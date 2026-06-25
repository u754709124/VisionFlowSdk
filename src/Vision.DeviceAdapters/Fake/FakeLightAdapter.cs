using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // Fake lights capture requested channel state for deterministic node tests.
    public sealed class FakeLightAdapter : ILightAdapter
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, LightChannelSetting> _settings;

        public FakeLightAdapter(string lightId)
        {
            if (string.IsNullOrWhiteSpace(lightId))
            {
                throw new ArgumentException("Light id is required.", "lightId");
            }

            LightId = lightId;
            _settings = new Dictionary<string, LightChannelSetting>(StringComparer.OrdinalIgnoreCase);
        }

        public string LightId { get; private set; }

        public Task SetAsync(LightChannelSetting setting, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (setting == null)
            {
                throw new ArgumentNullException("setting");
            }

            if (string.IsNullOrWhiteSpace(setting.ChannelName))
            {
                throw new ArgumentException("Light channel name is required.", "setting");
            }

            var copy = CloneSetting(setting);
            copy.LightId = string.IsNullOrWhiteSpace(copy.LightId) ? LightId : copy.LightId;

            lock (_gate)
            {
                _settings[copy.ChannelName] = copy;
            }

            return Task.FromResult(0);
        }

        public Task TurnOffAsync(string channelName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(channelName))
            {
                throw new ArgumentException("Light channel name is required.", "channelName");
            }

            lock (_gate)
            {
                _settings[channelName] = new LightChannelSetting
                {
                    LightId = LightId,
                    ChannelName = channelName,
                    IsEnabled = false,
                    Intensity = 0
                };
            }

            return Task.FromResult(0);
        }

        public IDictionary<string, LightChannelSetting> Snapshot()
        {
            lock (_gate)
            {
                var snapshot = new Dictionary<string, LightChannelSetting>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in _settings)
                {
                    snapshot[item.Key] = CloneSetting(item.Value);
                }

                return snapshot;
            }
        }

        private static LightChannelSetting CloneSetting(LightChannelSetting setting)
        {
            return new LightChannelSetting
            {
                LightId = setting.LightId,
                ChannelName = setting.ChannelName,
                IsEnabled = setting.IsEnabled,
                Intensity = setting.Intensity,
                DurationMs = setting.DurationMs
            };
        }
    }
}
