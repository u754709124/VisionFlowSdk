using System;
using System.Collections.Generic;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Runtime.Events;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 收集单次节点执行尝试实际读取的设置值；对象只在诊断启用时创建。
    /// </summary>
    internal sealed class NodeInputCaptureSession
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, NodeInputObservation> _observations =
            new Dictionary<string, NodeInputObservation>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _order = new List<string>();

        internal bool HasEntries
        {
            get
            {
                lock (_gate)
                    return _order.Count > 0;
            }
        }

        internal void RecordValue(string settingName, NodeSettingValue setting, object value)
        {
            Record(settingName, setting, value, null);
        }

        internal void RecordFailure(string settingName, NodeSettingValue setting, Exception error)
        {
            Record(settingName, setting, null, error);
        }

        internal IList<IDictionary<string, object>> CreateSnapshot()
        {
            lock (_gate)
            {
                var result = new List<IDictionary<string, object>>(_order.Count);
                foreach (string settingName in _order)
                {
                    NodeInputObservation observation = _observations[settingName];
                    var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        { FlowRuntimeDataKeys.SettingName, observation.SettingName },
                        { FlowRuntimeDataKeys.SettingMode, observation.SettingMode },
                        { FlowRuntimeDataKeys.SelectorScope, observation.SelectorScope },
                        { FlowRuntimeDataKeys.SelectorPath, observation.SelectorPath },
                        { FlowRuntimeDataKeys.Value, observation.Value },
                        { FlowRuntimeDataKeys.ResolutionError, observation.ResolutionError }
                    };
                    result.Add(item);
                }
                return result;
            }
        }

        private void Record(string settingName, NodeSettingValue setting, object value, Exception error)
        {
            string normalizedName = string.IsNullOrWhiteSpace(settingName)
                ? string.Empty
                : settingName.Trim();
            VariableSelector selector = setting == null ? null : setting.Selector;
            var observation = new NodeInputObservation
            {
                SettingName = normalizedName,
                SettingMode = setting == null ? "Missing" : setting.Mode.ToString(),
                SelectorScope = selector == null ? null : selector.Scope.ToString(),
                SelectorPath = selector == null || selector.Path == null
                    ? new List<string>()
                    : new List<string>(selector.Path),
                Value = value,
                ResolutionError = error == null
                    ? null
                    : error.GetBaseException().Message
            };

            lock (_gate)
            {
                if (!_observations.ContainsKey(normalizedName))
                    _order.Add(normalizedName);
                _observations[normalizedName] = observation;
            }
        }

        private sealed class NodeInputObservation
        {
            internal string SettingName { get; set; }
            internal string SettingMode { get; set; }
            internal string SelectorScope { get; set; }
            internal IList<string> SelectorPath { get; set; }
            internal object Value { get; set; }
            internal string ResolutionError { get; set; }
        }
    }
}
