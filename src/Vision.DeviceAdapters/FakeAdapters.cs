using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    public sealed class FakeAdapterErrorEventArgs : EventArgs
    {
        public FakeAdapterErrorEventArgs(Exception exception)
        {
            Exception = exception ?? throw new ArgumentNullException("exception");
        }

        public Exception Exception { get; private set; }
    }

    public sealed class FakeVisionImage : IVisionImage
    {
        public FakeVisionImage()
            : this(null, 640, 480, "Mono8", null)
        {
        }

        public FakeVisionImage(string imageId, int width, int height, string pixelFormat, byte[] data)
        {
            ImageId = string.IsNullOrWhiteSpace(imageId) ? Guid.NewGuid().ToString("N") : imageId;
            Width = width;
            Height = height;
            PixelFormat = string.IsNullOrWhiteSpace(pixelFormat) ? "Mono8" : pixelFormat;
            CreatedUtc = DateTime.UtcNow;
            Data = data ?? new byte[0];
            Metadata = new Dictionary<string, object>();
        }

        public string ImageId { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public string PixelFormat { get; private set; }

        public DateTime CreatedUtc { get; private set; }

        public byte[] Data { get; private set; }

        public IDictionary<string, object> Metadata { get; private set; }
    }

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

            var frame = new CameraFrameData
            {
                CameraId = CameraId,
                TriggerId = triggerId,
                FrameId = frameId,
                GrabTime = grabTime,
                Image = image
            };

            CopyMetadata(triggerMetadata, frame.Metadata);
            frame.Metadata["CameraId"] = CameraId;
            frame.Metadata["TriggerId"] = triggerId;
            frame.Metadata["FrameId"] = frameId;
            frame.Metadata["GrabTime"] = grabTime;

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

    public sealed class FakeMotionAdapter : IMotionAdapter
    {
        private readonly object _gate = new object();

        public FakeMotionAdapter(string motionId)
        {
            if (string.IsNullOrWhiteSpace(motionId))
            {
                throw new ArgumentException("Motion id is required.", "motionId");
            }

            MotionId = motionId;
            MoveDelayMs = 0;
        }

        public string MotionId { get; private set; }

        public string CurrentPosition { get; private set; }

        public int MoveDelayMs { get; set; }

        public MotionMessage LastMessage { get; private set; }

        public event EventHandler<MotionEventArgs> MotionEventReceived;

        public async Task MoveToAsync(string positionName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new ArgumentException("Position name is required.", "positionName");
            }

            if (MoveDelayMs > 0)
            {
                await Task.Delay(MoveDelayMs, cancellationToken).ConfigureAwait(false);
            }

            lock (_gate)
            {
                CurrentPosition = positionName;
            }

            RaiseMotionEvent(new MotionEventArgs
            {
                MotionId = MotionId,
                EventType = "MoveCompleted",
                PositionId = positionName
            });
        }

        public async Task WaitForInPositionAsync(string positionName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new ArgumentException("Position name is required.", "positionName");
            }

            string currentPosition;
            lock (_gate)
            {
                currentPosition = CurrentPosition;
            }

            if (!string.Equals(currentPosition, positionName, StringComparison.OrdinalIgnoreCase))
            {
                await MoveToAsync(positionName, cancellationToken).ConfigureAwait(false);
            }

            RaiseMotionEvent(new MotionEventArgs
            {
                MotionId = MotionId,
                EventType = "InPosition",
                PositionId = positionName
            });
        }

        public Task SendMessageAsync(MotionMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            var copy = CloneMessage(message);
            if (string.IsNullOrWhiteSpace(copy.MotionId))
            {
                copy.MotionId = MotionId;
            }

            lock (_gate)
            {
                LastMessage = copy;
            }

            return Task.FromResult(0);
        }

        public MotionMessage SnapshotLastMessage()
        {
            lock (_gate)
            {
                return LastMessage == null ? null : CloneMessage(LastMessage);
            }
        }

        public void RaiseMotionEvent(MotionEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            if (string.IsNullOrWhiteSpace(args.MotionId))
            {
                args.MotionId = MotionId;
            }

            var handler = MotionEventReceived;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        private static MotionMessage CloneMessage(MotionMessage message)
        {
            var clone = new MotionMessage
            {
                MessageType = message.MessageType,
                MotionId = message.MotionId,
                PositionId = message.PositionId,
                CaptureGroupId = message.CaptureGroupId,
                ScanGroupId = message.ScanGroupId,
                TokenId = message.TokenId,
                Result = message.Result
            };

            if (message.Metadata != null)
            {
                foreach (var item in message.Metadata)
                {
                    clone.Metadata[item.Key] = item.Value;
                }
            }

            return clone;
        }
    }

    public sealed class FakeRecipeAdapter : IRecipeAdapter
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, object> _defaultOutputs;

        public FakeRecipeAdapter(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                throw new ArgumentException("Recipe id is required.", "recipeId");
            }

            RecipeId = recipeId;
            _defaultOutputs = new Dictionary<string, object>();
        }

        public string RecipeId { get; private set; }

        public IDictionary<string, object> DefaultOutputs
        {
            get { return _defaultOutputs; }
        }

        public Task<RecipeRunResult> RunAsync(RecipeRunRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = new RecipeRunResult
            {
                IsSuccess = true,
                Status = "OK",
                Message = "Fake recipe completed."
            };

            result.Outputs["RecipeId"] = RecipeId;
            result.Outputs["RunTimeUtc"] = DateTime.UtcNow;

            lock (_gate)
            {
                foreach (var item in _defaultOutputs)
                {
                    result.Outputs[item.Key] = item.Value;
                }
            }

            if (request != null && request.Inputs != null)
            {
                foreach (var input in request.Inputs)
                {
                    result.Outputs["Input." + input.Key] = input.Value;
                }
            }

            return Task.FromResult(result);
        }
    }

    public sealed class FakeImageSaveAdapter : IImageSaveAdapter
    {
        private readonly object _gate = new object();
        private readonly List<ImageSaveRequest> _savedRequests;

        public FakeImageSaveAdapter(string saverId)
        {
            if (string.IsNullOrWhiteSpace(saverId))
            {
                throw new ArgumentException("Image saver id is required.", "saverId");
            }

            SaverId = saverId;
            BasePath = "fake://images";
            _savedRequests = new List<ImageSaveRequest>();
        }

        public string SaverId { get; private set; }

        public string BasePath { get; set; }

        public Task<ImageSaveResult> SaveAsync(ImageSaveRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.Image == null)
            {
                throw new ArgumentException("Image save request requires an image.", "request");
            }

            var directory = string.IsNullOrWhiteSpace(request.DirectoryPath) ? BasePath : request.DirectoryPath;
            var format = string.IsNullOrWhiteSpace(request.Format) ? "png" : request.Format.TrimStart('.');
            var fileName = string.IsNullOrWhiteSpace(request.FileName) ? request.Image.ImageId : request.FileName;
            if (fileName.IndexOf(".", StringComparison.Ordinal) < 0)
            {
                fileName = fileName + "." + format;
            }

            var result = new ImageSaveResult
            {
                IsSuccess = true,
                Path = CombinePath(directory, fileName),
                Message = "Fake image saved."
            };
            result.Metadata["SaverId"] = SaverId;
            result.Metadata["ImageId"] = request.Image.ImageId;

            lock (_gate)
            {
                _savedRequests.Add(CloneRequest(request));
            }

            return Task.FromResult(result);
        }

        public IList<ImageSaveRequest> SnapshotSavedRequests()
        {
            lock (_gate)
            {
                var snapshot = new List<ImageSaveRequest>();
                foreach (var request in _savedRequests)
                {
                    snapshot.Add(CloneRequest(request));
                }

                return snapshot;
            }
        }

        private static string CombinePath(string directory, string fileName)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                return fileName;
            }

            if (directory.EndsWith("/", StringComparison.Ordinal) || directory.EndsWith("\\", StringComparison.Ordinal))
            {
                return directory + fileName;
            }

            return directory + "/" + fileName;
        }

        private static ImageSaveRequest CloneRequest(ImageSaveRequest request)
        {
            var clone = new ImageSaveRequest
            {
                SaverId = request.SaverId,
                Image = request.Image,
                DirectoryPath = request.DirectoryPath,
                FileName = request.FileName,
                Format = request.Format
            };

            if (request.Metadata != null)
            {
                foreach (var item in request.Metadata)
                {
                    clone.Metadata[item.Key] = item.Value;
                }
            }

            return clone;
        }
    }

    public sealed class FakeDatabaseAdapter : IDatabaseAdapter
    {
        private readonly object _gate = new object();
        private readonly List<DatabaseSaveRequest> _savedRequests;

        public FakeDatabaseAdapter(string databaseId)
        {
            if (string.IsNullOrWhiteSpace(databaseId))
            {
                throw new ArgumentException("Database id is required.", "databaseId");
            }

            DatabaseId = databaseId;
            _savedRequests = new List<DatabaseSaveRequest>();
        }

        public string DatabaseId { get; private set; }

        public Task SaveAsync(DatabaseSaveRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            lock (_gate)
            {
                _savedRequests.Add(CloneRequest(request));
            }

            return Task.FromResult(0);
        }

        public IList<DatabaseSaveRequest> SnapshotSavedRequests()
        {
            lock (_gate)
            {
                var snapshot = new List<DatabaseSaveRequest>();
                foreach (var request in _savedRequests)
                {
                    snapshot.Add(CloneRequest(request));
                }

                return snapshot;
            }
        }

        private static DatabaseSaveRequest CloneRequest(DatabaseSaveRequest request)
        {
            var clone = new DatabaseSaveRequest
            {
                DatabaseId = request.DatabaseId,
                TableName = request.TableName
            };

            if (request.Values != null)
            {
                foreach (var item in request.Values)
                {
                    clone.Values[item.Key] = item.Value;
                }
            }

            if (request.Metadata != null)
            {
                foreach (var item in request.Metadata)
                {
                    clone.Metadata[item.Key] = item.Value;
                }
            }

            return clone;
        }
    }
}
