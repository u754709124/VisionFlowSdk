using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

namespace Vision.Flow.Demo.WinForms
{
    // Event helpers keep runtime event display, token summaries, and output previews together.
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
            _outputSummary.Text = _lastOutputSummary ?? "Image: waiting\r\nFrameId: -\r\nRecipeResult: -\r\nDatabaseSave: -";
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
                !runtimeEvent.Data.ContainsKey("VariableName"))
            {
                return;
            }

            var variableName = Convert.ToString(runtimeEvent.Data["VariableName"]);
            object value = runtimeEvent.Data.ContainsKey("Value") ? runtimeEvent.Data["Value"] : null;

            var image = value as IVisionImage;
            if (image != null)
            {
                _lastImageSummary = BuildImageSummary(image);
            }

            if (string.Equals(variableName, "cam_callback_1.FrameId", StringComparison.OrdinalIgnoreCase))
            {
                _lastFrameId = Convert.ToString(value);
            }

            if (string.Equals(variableName, "image_save_1.ImagePath", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variableName, "image_save_1.ResultImagePath", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variableName, "recipe_1.IsOk", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(variableName, "db_save_1.Saved", StringComparison.OrdinalIgnoreCase))
            {
                BuildOutputSummary();
            }
            else if (image != null || !string.IsNullOrWhiteSpace(_lastFrameId))
            {
                BuildOutputSummary();
            }

            if (image != null)
            {
                _imagePreview.Invalidate();
            }
        }

        private void BuildOutputSummary()
        {
            var imagePath = FindLatestOutput("image_save_1.ImagePath");
            var resultImagePath = FindLatestOutput("image_save_1.ResultImagePath");
            var isOk = FindLatestOutput("recipe_1.IsOk");
            var saved = FindLatestOutput("db_save_1.Saved");
            _lastOutputSummary =
                "Image: " + (_lastImageSummary ?? "-") + "\r\n" +
                "FrameId: " + (_lastFrameId ?? "-") + "\r\n" +
                "RecipeResult: " + (isOk == null ? "-" : "IsOk=" + isOk) + "\r\n" +
                "ImagePath: " + (imagePath ?? "-") + "\r\n" +
                "ResultImagePath: " + (resultImagePath ?? "-") + "\r\n" +
                "DatabaseSave: " + (saved ?? "-");
            _outputSummary.Text = _lastOutputSummary;
        }

        private static string BuildImageSummary(IVisionImage image)
        {
            if (image == null)
            {
                return null;
            }

            byte[] bytes;
            var byteText = image.TryGetBytes(out bytes) && bytes != null
                ? bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes"
                : "bytes unavailable";
            var storageText = image.NativeImage == null ? "managed" : "native";
            var disposedText = image.IsDisposed ? ", disposed" : string.Empty;
            return image.Width.ToString(CultureInfo.InvariantCulture) +
                "x" +
                image.Height.ToString(CultureInfo.InvariantCulture) +
                " " +
                image.PixelFormat +
                ", " +
                byteText +
                ", " +
                storageText +
                disposedText;
        }

        private object FindLatestOutput(string variableName)
        {
            for (var row = 0; row < _eventGrid.Rows.Count; row++)
            {
                // The event grid is for humans; runtime output values are easier to capture directly.
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
                    runtimeEvent.Data.ContainsKey("VariableName"))
                {
                    lock (_gate)
                    {
                        _outputs[Convert.ToString(runtimeEvent.Data["VariableName"])] =
                            runtimeEvent.Data.ContainsKey("Value") ? runtimeEvent.Data["Value"] : null;
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
