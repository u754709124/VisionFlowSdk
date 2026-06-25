using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // 模拟相机在不依赖厂商 SDK 的情况下模拟参数存储、软触发和帧回调。
    public sealed class FakeCameraAdapter : ICameraAdapter
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, object> _parameters;
        private readonly List<CameraParameterDescriptor> _parameterDescriptors;
        private int _frameSequence;

        public FakeCameraAdapter(string cameraId)
        {
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                throw new ArgumentException("Camera id is required.", "cameraId");
            }

            CameraId = cameraId;
            FrameDelayMs = 10;
            ImageWidth = 640;
            ImageHeight = 480;
            PixelFormat = "Mono8";
            _parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _parameterDescriptors = new List<CameraParameterDescriptor>();

            AddParameterDescriptor("ExposureTime", "Exposure Time", "Double", "us", 1000.0, 10.0, 1000000.0);
            AddParameterDescriptor("Gain", "Gain", "Double", "dB", 1.0, 0.0, 24.0);
        }

        public string CameraId { get; private set; }

        public int FrameDelayMs { get; set; }

        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }

        public string PixelFormat { get; set; }

        public bool ReturnBeforeFrameArrived { get; set; }

        public Exception LastError { get; private set; }

        public Task LastBackgroundFrameTask { get; private set; }

        public event EventHandler<CameraFrameArrivedEventArgs> FrameArrived;

        public event EventHandler<FakeAdapterErrorEventArgs> AdapterError;

        public IReadOnlyList<CameraParameterDescriptor> GetParameterDescriptors()
        {
            lock (_gate)
            {
                var descriptors = new List<CameraParameterDescriptor>();
                foreach (var descriptor in _parameterDescriptors)
                {
                    descriptors.Add(CloneParameterDescriptor(descriptor));
                }

                return descriptors;
            }
        }

        public Task SetParameterAsync(string parameterName, object value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("Parameter name is required.", "parameterName");
            }

            lock (_gate)
            {
                _parameters[parameterName] = value;
            }

            return Task.FromResult(0);
        }

        public Task<object> GetParameterAsync(string parameterName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                throw new ArgumentException("Parameter name is required.", "parameterName");
            }

            lock (_gate)
            {
                object value;
                if (_parameters.TryGetValue(parameterName, out value))
                {
                    return Task.FromResult(value);
                }
            }

            throw new KeyNotFoundException("Camera parameter was not found: " + parameterName);
        }

        public Task SoftTriggerAsync(CameraTriggerContext triggerContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = triggerContext ?? new CameraTriggerContext();
            if (string.IsNullOrWhiteSpace(context.CameraId))
            {
                context.CameraId = CameraId;
            }

            if (!string.Equals(context.CameraId, CameraId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Trigger camera id does not match this adapter: " + context.CameraId);
            }

            if (string.IsNullOrWhiteSpace(context.TriggerId))
            {
                context.TriggerId = Guid.NewGuid().ToString("N");
            }

            var triggerId = context.TriggerId;
            var triggerMetadata = CopyDictionary(context.Metadata);
            if (ReturnBeforeFrameArrived)
            {
                LastBackgroundFrameTask = ProduceFrameInBackgroundAsync(triggerId, triggerMetadata, cancellationToken);
                return Task.FromResult(0);
            }

            return ProduceFrameAsync(triggerId, triggerMetadata, cancellationToken);
        }

        private async Task ProduceFrameInBackgroundAsync(
            string triggerId,
            IDictionary<string, object> triggerMetadata,
            CancellationToken cancellationToken)
        {
            try
            {
                await ProduceFrameAsync(triggerId, triggerMetadata, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ReportAdapterError(new TimeoutException("Fake camera frame production was canceled."));
                }
            }
            catch (Exception ex)
            {
                ReportAdapterError(ex);
            }
        }

        private async Task ProduceFrameAsync(
            string triggerId,
            IDictionary<string, object> triggerMetadata,
            CancellationToken cancellationToken)
        {
            if (FrameDelayMs > 0)
            {
                await Task.Delay(FrameDelayMs, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var frame = CreateFrame(triggerId, triggerMetadata);
            RaiseFrameArrived(frame);
        }

        private void ReportAdapterError(Exception exception)
        {
            LastError = exception;
            var handler = AdapterError;
            if (handler != null)
            {
                handler(this, new FakeAdapterErrorEventArgs(exception));
            }
        }

        private void AddParameterDescriptor(
            string parameterName,
            string displayName,
            string valueType,
            string unit,
            object defaultValue,
            object minimum,
            object maximum)
        {
            _parameterDescriptors.Add(new CameraParameterDescriptor
            {
                ParameterName = parameterName,
                DisplayName = displayName,
                ValueType = valueType,
                Unit = unit,
                IsWritable = true,
                DefaultValue = defaultValue,
                Minimum = minimum,
                Maximum = maximum
            });
            _parameters[parameterName] = defaultValue;
        }

        private CameraFrameData CreateFrame(string triggerId, IDictionary<string, object> triggerMetadata)
        {
            var frameNumber = Interlocked.Increment(ref _frameSequence);
            var grabTime = DateTime.UtcNow;
            var frameId = CameraId + "-" + frameNumber.ToString(CultureInfo.InvariantCulture);
            var image = new FakeVisionImage(frameId, ImageWidth, ImageHeight, PixelFormat, null);
            image.Metadata[FlowMetadataKeys.ImageKind] = image.ImageKind;

            var frame = new CameraFrameData
            {
                CameraId = CameraId,
                TriggerId = triggerId,
                FrameId = frameId,
                GrabTime = grabTime,
                Image = image
            };

            CopyMetadata(triggerMetadata, frame.Metadata);
            frame.Metadata[FlowMetadataKeys.CameraId] = CameraId;
            frame.Metadata[FlowMetadataKeys.TriggerId] = triggerId;
            frame.Metadata[FlowMetadataKeys.FrameId] = frameId;
            frame.Metadata[FlowMetadataKeys.GrabTime] = grabTime;

            CopyMetadata(frame.Metadata, image.Metadata);
            return frame;
        }

        private void RaiseFrameArrived(CameraFrameData frame)
        {
            var handler = FrameArrived;
            if (handler != null)
            {
                handler(this, new CameraFrameArrivedEventArgs(frame));
            }
        }

        private static CameraParameterDescriptor CloneParameterDescriptor(CameraParameterDescriptor descriptor)
        {
            return new CameraParameterDescriptor
            {
                ParameterName = descriptor.ParameterName,
                DisplayName = descriptor.DisplayName,
                ValueType = descriptor.ValueType,
                Unit = descriptor.Unit,
                IsWritable = descriptor.IsWritable,
                Minimum = descriptor.Minimum,
                Maximum = descriptor.Maximum,
                DefaultValue = descriptor.DefaultValue
            };
        }

        private static IDictionary<string, object> CopyDictionary(IDictionary<string, object> source)
        {
            var copy = new Dictionary<string, object>();
            CopyMetadata(source, copy);
            return copy;
        }

        private static void CopyMetadata(IDictionary<string, object> source, IDictionary<string, object> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (var item in source)
            {
                target[item.Key] = item.Value;
            }
        }
    }
}
