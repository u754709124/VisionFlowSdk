using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core.Contracts.Devices;

namespace Vision.Flow.Core.Runtime.Events
{
    /// <summary>
    /// 在转发前复制并轻量化事件数据，保证下游不会间接持有图像或其他可释放资源。
    /// </summary>
    public sealed class SanitizingFlowEventSink : IFlowEventSink
    {
        private readonly IFlowEventSink _inner;
        private readonly FlowEventSinkOptions _options;

        /// <summary>
        /// 创建轻量事件包装器；包装器不拥有也不释放内部事件出口。
        /// </summary>
        public SanitizingFlowEventSink(IFlowEventSink inner, FlowEventSinkOptions options = null)
        {
            _inner = inner ?? throw new ArgumentNullException("inner");
            _options = options ?? new FlowEventSinkOptions();
        }

        /// <summary>
        /// 创建事件快照并将快照转发给内部出口。
        /// </summary>
        public Task PublishAsync(FlowRuntimeEvent runtimeEvent, CancellationToken cancellationToken)
        {
            if (runtimeEvent == null)
            {
                throw new ArgumentNullException("runtimeEvent");
            }

            return _inner.PublishAsync(CreateSnapshot(runtimeEvent), cancellationToken);
        }

        private FlowRuntimeEvent CreateSnapshot(FlowRuntimeEvent source)
        {
            var snapshot = new FlowRuntimeEvent
            {
                TimestampUtc = source.TimestampUtc,
                EventType = source.EventType,
                FlowId = source.FlowId,
                FlowRunId = source.FlowRunId,
                TokenId = source.TokenId,
                NodeId = source.NodeId,
                NodeName = source.NodeName,
                State = source.State,
                OutputPort = source.OutputPort,
                Message = Truncate(source.Message),
                ElapsedMs = source.ElapsedMs,
                Data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };
            if (source.Data == null)
            {
                return snapshot;
            }

            foreach (var pair in source.Data)
            {
                snapshot.Data[pair.Key] = CreateValueSnapshot(pair.Value, 0);
            }

            return snapshot;
        }

        private object CreateValueSnapshot(object value, int depth)
        {
            if (value == null || value is bool || value is char ||
                value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal ||
                value is DateTime || value is DateTimeOffset || value is TimeSpan ||
                value is Guid || value.GetType().IsEnum)
            {
                return value;
            }

            var text = value as string;
            if (text != null)
            {
                return Truncate(text);
            }

            var image = value as IVisionImage;
            if (image != null)
            {
                return new FlowRuntimeValueSummary
                {
                    TypeName = value.GetType().FullName,
                    Description = string.Format(
                        CultureInfo.InvariantCulture,
                        "ImageId={0}, {1}x{2}, PixelFormat={3}",
                        image.ImageId,
                        image.Width,
                        image.Height,
                        image.PixelFormat),
                    IsResource = true
                };
            }

            var frame = value as CameraFrameData;
            if (frame != null)
            {
                return new FlowRuntimeValueSummary
                {
                    TypeName = value.GetType().FullName,
                    Description = "CameraId=" + frame.CameraId + ", CaptureFrameId=" + frame.CaptureFrameId,
                    IsResource = frame.Image != null
                };
            }

            var bytes = value as byte[];
            if (bytes != null)
            {
                return new FlowRuntimeValueSummary
                {
                    TypeName = typeof(byte[]).FullName,
                    Description = "Binary payload",
                    Size = bytes.LongLength,
                    IsResource = false
                };
            }

            if (value is IDisposable)
            {
                return CreateOpaqueSummary(value, true, null);
            }

            if (depth >= Math.Max(0, _options.MaxDataDepth))
            {
                return CreateOpaqueSummary(value, false, null);
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var count = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (count++ >= Math.Max(0, _options.MaxCollectionItems))
                    {
                        break;
                    }

                    copy[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] =
                        CreateValueSnapshot(entry.Value, depth + 1);
                }

                return copy;
            }

            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                var copy = new List<object>();
                foreach (var item in enumerable)
                {
                    if (copy.Count >= Math.Max(0, _options.MaxCollectionItems))
                    {
                        break;
                    }

                    copy.Add(CreateValueSnapshot(item, depth + 1));
                }

                return copy;
            }

            return CreateOpaqueSummary(value, false, null);
        }

        private static FlowRuntimeValueSummary CreateOpaqueSummary(object value, bool isResource, long? size)
        {
            return new FlowRuntimeValueSummary
            {
                TypeName = value.GetType().FullName,
                Description = isResource ? "Disposable resource" : "Opaque runtime value",
                Size = size,
                IsResource = isResource
            };
        }

        private string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= Math.Max(0, _options.MaxStringLength))
            {
                return value;
            }

            return value.Substring(0, Math.Max(0, _options.MaxStringLength));
        }
    }
}
