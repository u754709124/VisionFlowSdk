using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vision.Flow.Nodes;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.CameraFrames;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Runtime.Queues;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Demo.WinForms
{
    // �¼������������д��������¼���ʾ��Token ժҪ�����Ԥ����
    public sealed partial class MainForm
    {
        private void AddEvent(string source, string eventName, string detail)
        {
            _eventSequence++;
            _eventGrid.Rows.Insert(0, _eventSequence.ToString(), DateTime.Now.ToString("HH:mm:ss.fff"), source, eventName, detail);
            if (_eventGrid.Rows.Count > 200)
            {
                _eventGrid.Rows.RemoveAt(_eventGrid.Rows.Count - 1);
            }
        }

        private void RefreshTokenSummary(string entryName)
        {
            _tokenList.Items.Clear();
            var tokenId = _lastToken == null ? "token-" + _eventSequence.ToString("000") : _lastToken.TokenId;
            _tokenList.Items.Add(new ListViewItem(new[] { tokenId, _runner != null && _runner.IsRunning ? "Completed" : "Stopped", entryName }));
            _outputSummary.Text = _lastOutputSummary ?? "Result: waiting\r\nConditionMatched: -\r\nLog: -";
            _imagePreview.Invalidate();
        }

        private void HandleRuntimeEvent(FlowRuntimeEvent runtimeEvent)
        {
            if (runtimeEvent == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<FlowRuntimeEvent>(HandleRuntimeEvent), runtimeEvent);
                return;
            }

            var source = string.IsNullOrWhiteSpace(runtimeEvent.NodeId) ? runtimeEvent.FlowId : runtimeEvent.NodeId;
            var detail = string.IsNullOrWhiteSpace(runtimeEvent.Message) ? runtimeEvent.OutputPort : runtimeEvent.Message;
            AddEvent(source, runtimeEvent.EventType.ToString(), detail);
            UpdateOutputSummary(runtimeEvent);
        }

        private void UpdateOutputSummary(FlowRuntimeEvent runtimeEvent)
        {
            if (runtimeEvent.EventType != FlowRuntimeEventType.OutputProduced ||
                runtimeEvent.Data == null ||
                !runtimeEvent.Data.ContainsKey(FlowRuntimeDataKeys.VariableName))
            {
                return;
            }

            var variableName = Convert.ToString(runtimeEvent.Data[FlowRuntimeDataKeys.VariableName]);
            object value = runtimeEvent.Data.ContainsKey(FlowRuntimeDataKeys.Value) ? runtimeEvent.Data[FlowRuntimeDataKeys.Value] : null;

            BuildOutputSummary();
            _imagePreview.Invalidate();
        }

        private void BuildOutputSummary()
        {
            var result = FindLatestOutput("set_result.Value");
            var matched = FindLatestOutput("condition_1.IsMatched");
            var okMessage = FindLatestOutput("log_ok.Message");
            var ngMessage = FindLatestOutput("log_ng.Message");
            _lastOutputSummary =
                "Result: " + (result ?? "-") + "\r\n" +
                "ConditionMatched: " + (matched ?? "-") + "\r\n" +
                "OK Log: " + (okMessage ?? "-") + "\r\n" +
                "NG Log: " + (ngMessage ?? "-");
            _outputSummary.Text = _lastOutputSummary;
        }

        private object FindLatestOutput(string variableName)
        {
            for (var row = 0; row < _eventGrid.Rows.Count; row++)
            {
                // �¼���������˹��鿴���������ֱֵ�Ӵ��¼����زɼ����ɿ���
            }

            return _eventSink.TryGetOutput(variableName);
        }

        private sealed class UiFlowEventSink : IFlowEventSink
        {
            private readonly object _gate = new object();
            private readonly Action<FlowRuntimeEvent> _onEvent;
            private readonly Dictionary<string, object> _outputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public UiFlowEventSink(Action<FlowRuntimeEvent> onEvent)
            {
                _onEvent = onEvent;
            }

            public Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (runtimeEvent == null)
                {
                    throw new ArgumentNullException("runtimeEvent");
                }

                if (runtimeEvent.EventType == FlowRuntimeEventType.OutputProduced &&
                    runtimeEvent.Data != null &&
                    runtimeEvent.Data.ContainsKey(FlowRuntimeDataKeys.VariableName))
                {
                    lock (_gate)
                    {
                        _outputs[Convert.ToString(runtimeEvent.Data[FlowRuntimeDataKeys.VariableName])] =
                            runtimeEvent.Data.ContainsKey(FlowRuntimeDataKeys.Value) ? runtimeEvent.Data[FlowRuntimeDataKeys.Value] : null;
                    }
                }

                _onEvent(runtimeEvent);
                return Task.FromResult(0);
            }

            public object TryGetOutput(string variableName)
            {
                lock (_gate)
                {
                    object value;
                    return _outputs.TryGetValue(variableName, out value) ? value : null;
                }
            }

            public void Clear()
            {
                lock (_gate)
                {
                    _outputs.Clear();
                }
            }
        }
    }
}
