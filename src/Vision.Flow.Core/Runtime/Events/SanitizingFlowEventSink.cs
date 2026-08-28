using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
                Timestamp = source.Timestamp,
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
                snapshot.Data[pair.Key] = CreateValueSnapshot(
                    pair.Value,
                    0,
                    new HashSet<object>(ReferenceEqualityComparer.Instance));
            }

            return snapshot;
        }

        private object CreateValueSnapshot(object value, int depth, ISet<object> ancestors)
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

            var existingSummary = value as FlowRuntimeValueSummary;
            if (existingSummary != null)
            {
                return new FlowRuntimeValueSummary
                {
                    TypeName = existingSummary.TypeName,
                    Description = Truncate(existingSummary.Description),
                    Size = existingSummary.Size,
                    IsResource = existingSummary.IsResource
                };
            }

            var existingUnevaluated = value as FlowRuntimeUnevaluatedValue;
            if (existingUnevaluated != null)
            {
                return new FlowRuntimeUnevaluatedValue
                {
                    TypeName = existingUnevaluated.TypeName,
                    Reason = existingUnevaluated.Reason,
                    IsResource = existingUnevaluated.IsResource
                };
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

            if (IsHTupleType(value.GetType()))
            {
                return CreateUnevaluatedValue(value.GetType());
            }

            if (depth >= Math.Max(0, _options.MaxDataDepth))
            {
                return CreateOpaqueSummary(value, false, null);
            }

            bool tracksReference = !value.GetType().IsValueType;
            if (tracksReference && !ancestors.Add(value))
            {
                return CreateOpaqueSummary(value, false, null, "Cyclic reference");
            }

            try
            {
                return CreateStructuredSnapshot(value, depth, ancestors);
            }
            finally
            {
                if (tracksReference)
                {
                    ancestors.Remove(value);
                }
            }
        }

        private object CreateStructuredSnapshot(object value, int depth, ISet<object> ancestors)
        {

            object dictionarySnapshot;
            if (TryCreateDictionarySnapshot(
                value,
                depth,
                ancestors,
                out dictionarySnapshot))
            {
                return dictionarySnapshot;
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

                    copy.Add(CreateValueSnapshot(item, depth + 1, ancestors));
                }

                return copy;
            }

            return CreateObjectSnapshot(value, depth, ancestors);
        }

        private bool TryCreateDictionarySnapshot(
            object value,
            int depth,
            ISet<object> ancestors,
            out object result)
        {
            var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                int count = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (count++ >= Math.Max(0, _options.MaxCollectionItems))
                        break;
                    copy[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] =
                        CreateValueSnapshot(entry.Value, depth + 1, ancestors);
                }
                result = copy;
                return true;
            }

            bool isGenericDictionary = value.GetType()
                .GetInterfaces()
                .Where(x => x.IsGenericType)
                .Select(x => x.GetGenericTypeDefinition().FullName)
                .Any(x => string.Equals(x, "System.Collections.Generic.IDictionary`2", StringComparison.Ordinal) ||
                    string.Equals(x, "System.Collections.Generic.IReadOnlyDictionary`2", StringComparison.Ordinal));
            if (!isGenericDictionary)
            {
                result = null;
                return false;
            }

            int itemCount = 0;
            foreach (object entry in (IEnumerable)value)
            {
                if (itemCount++ >= Math.Max(0, _options.MaxCollectionItems))
                    break;
                Type entryType = entry.GetType();
                PropertyInfo keyProperty = entryType.GetProperty("Key");
                PropertyInfo valueProperty = entryType.GetProperty("Value");
                if (keyProperty == null || valueProperty == null)
                    continue;
                object key = keyProperty.GetValue(entry, null);
                object entryValue = valueProperty.GetValue(entry, null);
                copy[Convert.ToString(key, CultureInfo.InvariantCulture)] =
                    CreateValueSnapshot(entryValue, depth + 1, ancestors);
            }
            result = copy;
            return true;
        }

        private object CreateObjectSnapshot(object value, int depth, ISet<object> ancestors)
        {
            Type type = value.GetType();
            PropertyInfo[] properties = GetPublicProperties(type)
                .Where(x => x.GetIndexParameters().Length == 0 && x.GetGetMethod(false) != null)
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x
                    .OrderBy(property => GetInheritanceDistance(type, property.DeclaringType))
                    .ThenBy(property => property.Name, StringComparer.Ordinal)
                    .First())
                .ToArray();
            var propertyNames = new HashSet<string>(
                properties.Select(x => x.Name),
                StringComparer.OrdinalIgnoreCase);
            FieldInfo[] fields = GetPublicFields(type)
                .Where(x => !propertyNames.Contains(x.Name))
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x
                    .OrderBy(field => GetInheritanceDistance(type, field.DeclaringType))
                    .ThenBy(field => field.Name, StringComparer.Ordinal)
                    .First())
                .ToArray();
            MemberInfo[] members = properties
                .Cast<MemberInfo>()
                .Concat(fields)
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToArray();
            if (members.Length == 0)
            {
                return CreateOpaqueSummary(value, false, null);
            }

            var snapshot = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            int maximumMembers = Math.Max(0, _options.MaxCollectionItems);
            foreach (MemberInfo member in members.Take(maximumMembers))
            {
                snapshot[member.Name] = ReadMemberSnapshot(value, member, depth, ancestors);
            }

            return new FlowRuntimeObjectSnapshot
            {
                TypeName = type.FullName,
                IsResource = value is IDisposable,
                Members = snapshot
            };
        }

        private object ReadMemberSnapshot(object instance, MemberInfo member, int depth, ISet<object> ancestors)
        {
            var property = member as PropertyInfo;
            if (property != null && IsHTupleType(property.PropertyType))
            {
                return CreateUnevaluatedValue(property.PropertyType);
            }

            try
            {
                object value = property != null
                    ? property.GetValue(instance, null)
                    : ((FieldInfo)member).GetValue(instance);
                return CreateValueSnapshot(value, depth + 1, ancestors);
            }
            catch (Exception exception)
            {
                Exception actual = exception is TargetInvocationException && exception.InnerException != null
                    ? exception.InnerException
                    : exception;
                return Truncate("<读取失败: " + actual.GetType().Name + ">");
            }
        }

        private static FlowRuntimeUnevaluatedValue CreateUnevaluatedValue(Type type)
        {
            return new FlowRuntimeUnevaluatedValue
            {
                TypeName = type.FullName ?? type.Name,
                Reason = "HTupleNotEvaluated",
                IsResource = typeof(IDisposable).IsAssignableFrom(type)
            };
        }

        private static bool IsHTupleType(Type type)
        {
            return type != null && string.Equals(
                type.FullName,
                "HalconDotNet.HTuple",
                StringComparison.Ordinal);
        }

        private static PropertyInfo[] GetPublicProperties(Type type)
        {
            try
            {
                return type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            }
            catch
            {
                return new PropertyInfo[0];
            }
        }

        private static FieldInfo[] GetPublicFields(Type type)
        {
            try
            {
                return type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            }
            catch
            {
                return new FieldInfo[0];
            }
        }

        private static int GetInheritanceDistance(Type runtimeType, Type declaringType)
        {
            int distance = 0;
            for (Type current = runtimeType; current != null; current = current.BaseType)
            {
                if (current == declaringType)
                {
                    return distance;
                }

                distance++;
            }

            return int.MaxValue;
        }

        private static FlowRuntimeValueSummary CreateOpaqueSummary(
            object value,
            bool isResource,
            long? size,
            string description = null)
        {
            return new FlowRuntimeValueSummary
            {
                TypeName = value.GetType().FullName,
                Description = description ?? (isResource ? "Disposable resource" : "Opaque runtime value"),
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

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
