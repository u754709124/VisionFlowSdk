using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;
using static Vision.Flow.Nodes.GroupScanFusionNodeHelpers;

namespace Vision.Flow.Nodes
{
    // 共享分组辅助方法保持绑定、元数据和索引校验行为一致。
    internal enum DuplicateItemPolicy
    {
        Error = 0,
        Ignore = 1,
        Replace = 2
    }

    internal static class GroupScanFusionNodeHelpers
    {
        internal static NodeSettingDescriptor CreateStringSetting(
            string name,
            string displayName,
            string defaultValue,
            bool isRequired,
            string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "String",
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        internal static NodeSettingDescriptor CreateIntSetting(
            string name,
            string displayName,
            int defaultValue,
            bool isRequired,
            string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "Int32",
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        internal static NodeSettingDescriptor CreateBoolSetting(
            string name,
            string displayName,
            bool defaultValue,
            bool isRequired,
            string description)
        {
            return new NodeSettingDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = "Boolean",
                DefaultValue = defaultValue,
                IsRequired = isRequired,
                Description = description
            };
        }

        internal static NodeOutputDescriptor CreateOutput(string name, string displayName, string dataType, string description)
        {
            return new NodeOutputDescriptor
            {
                Name = name,
                DisplayName = displayName,
                DataType = dataType,
                Description = description
            };
        }

        internal static CameraFrameData ResolveFrame(FlowExecutionContext context)
        {
            return ResolveFrame(context, null);
        }

        internal static CameraFrameData ResolveFrame(FlowExecutionContext context, string bindingExpression)
        {
            var value = ResolveConfiguredValue(context, "Frame", bindingExpression);
            if (value == null)
            {
                object tokenValue;
                if (context.Token.TryGet("Frame", out tokenValue))
                {
                    value = tokenValue;
                }
            }

            var frame = value as CameraFrameData;
            if (value != null && frame == null)
            {
                throw new InvalidCastException("Frame must be CameraFrameData.");
            }

            return frame;
        }

        internal static IVisionImage ResolveImage(FlowExecutionContext context)
        {
            return ResolveImage(context, null);
        }

        internal static IVisionImage ResolveImage(FlowExecutionContext context, string bindingExpression)
        {
            var value = ResolveConfiguredValue(context, "Image", bindingExpression);
            if (value == null)
            {
                object tokenValue;
                if (context.Token.TryGet("Image", out tokenValue))
                {
                    value = tokenValue;
                }
            }

            return AdapterNodeHelpers.ResolveVisionImage(value, "Image");
        }

        internal static string ResolveCaptureGroupId(FlowExecutionContext context, string defaultValue, string bindingExpression)
        {
            var value = ResolveStringValue(context, "CaptureGroupId", defaultValue, bindingExpression);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            object tokenValue;
            if (context.Token.TryGet("CaptureGroupId", out tokenValue))
            {
                return tokenValue == null ? null : Convert.ToString(tokenValue, CultureInfo.InvariantCulture);
            }

            return context.Token.CaptureGroupId;
        }

        internal static string ResolveScanGroupId(FlowExecutionContext context, string defaultValue)
        {
            return ResolveScanGroupId(context, defaultValue, null);
        }

        internal static string ResolveScanGroupId(FlowExecutionContext context, string defaultValue, string bindingExpression)
        {
            var value = ResolveStringValue(context, "ScanGroupId", defaultValue, bindingExpression);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            object tokenValue;
            if (context.Token.TryGet("ScanGroupId", out tokenValue))
            {
                return tokenValue == null ? null : Convert.ToString(tokenValue, CultureInfo.InvariantCulture);
            }

            return context.Token.ScanGroupId;
        }

        internal static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            return ResolveStringValue(context, name, defaultValue, null);
        }

        internal static string ResolveStringValue(
            FlowExecutionContext context,
            string name,
            string defaultValue,
            string bindingExpression)
        {
            var value = ResolveConfiguredValue(context, name, bindingExpression);
            return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        internal static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
        {
            int value;
            return TryResolveInt32(context, name, out value) ? value : defaultValue;
        }

        internal static bool TryResolveInt32(FlowExecutionContext context, string name, out int value)
        {
            return TryResolveInt32(context, name, null, out value);
        }

        internal static bool TryResolveInt32(FlowExecutionContext context, string name, string bindingExpression, out int value)
        {
            object inputValue = ResolveConfiguredValue(context, name, bindingExpression);
            if (inputValue == null)
            {
                object tokenValue;
                if (context.Token.TryGet(name, out tokenValue))
                {
                    inputValue = tokenValue;
                }
            }

            if (inputValue == null)
            {
                value = 0;
                return false;
            }

            value = Convert.ToInt32(inputValue, CultureInfo.InvariantCulture);
            return true;
        }

        internal static bool ResolveBoolean(FlowExecutionContext context, string name, bool defaultValue)
        {
            var value = ResolveConfiguredValue(context, name, null);
            return value == null ? defaultValue : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        internal static DuplicateItemPolicy ResolveDuplicatePolicy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DuplicateItemPolicy.Error;
            }

            DuplicateItemPolicy policy;
            if (Enum.TryParse(value, true, out policy))
            {
                return policy;
            }

            throw new InvalidOperationException("DuplicatePolicy must be Error, Ignore, or Replace.");
        }

        internal static bool HasContinuousIndexes(IEnumerable<int> indexes, int expectedCount, int firstIndex)
        {
            if (indexes == null || expectedCount <= 0)
            {
                return false;
            }

            var lookup = new HashSet<int>(indexes);
            for (var index = 0; index < expectedCount; index++)
            {
                if (!lookup.Contains(firstIndex + index))
                {
                    return false;
                }
            }

            return true;
        }

        internal static object ResolveConfiguredValue(FlowExecutionContext context, string inputName, string bindingExpression)
        {
            var value = context.GetInputValue(inputName);
            if (value != null)
            {
                return value;
            }

            return ResolveBindingExpression(context, bindingExpression);
        }

        internal static void CopyTokenMetadata(FlowToken token, IDictionary<string, object> metadata)
        {
            if (token == null || metadata == null)
            {
                return;
            }

            metadata["TokenId"] = token.TokenId;
            metadata["ProductId"] = token.ProductId;
            metadata["WorkpieceId"] = token.WorkpieceId;
            metadata["PositionId"] = token.PositionId;
            metadata["CaptureGroupId"] = token.CaptureGroupId;
            metadata["ScanGroupId"] = token.ScanGroupId;
            metadata["FrameId"] = token.FrameId;
            CopyDictionary(token.Metadata, metadata);
        }

        internal static void CopyDictionary(IDictionary<string, object> source, IDictionary<string, object> target)
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

        internal static string SafeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Replace(" ", "_");
        }

        private static object ResolveBindingExpression(FlowExecutionContext context, string bindingExpression)
        {
            if (string.IsNullOrWhiteSpace(bindingExpression))
            {
                return null;
            }

            var path = NormalizeBindingPath(bindingExpression);
            if (path.StartsWith("token.", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveTokenPath(context.Token, path.Substring("token.".Length));
            }

            return context.ResolveBinding(VariableBinding.ForExpression(bindingExpression));
        }

        private static string NormalizeBindingPath(string bindingExpression)
        {
            var value = bindingExpression.Trim();
            if (value.StartsWith("{{", StringComparison.Ordinal) && value.EndsWith("}}", StringComparison.Ordinal))
            {
                value = value.Substring(2, value.Length - 4).Trim();
            }

            return value;
        }

        private static object ResolveTokenPath(FlowToken token, string tokenPath)
        {
            if (token == null || string.IsNullOrWhiteSpace(tokenPath))
            {
                return null;
            }

            if (tokenPath.StartsWith("Values.", StringComparison.OrdinalIgnoreCase))
            {
                object value;
                return token.TryGet(tokenPath.Substring("Values.".Length), out value) ? value : null;
            }

            if (tokenPath.StartsWith("Metadata.", StringComparison.OrdinalIgnoreCase))
            {
                object value;
                return token.Metadata != null && token.Metadata.TryGetValue(tokenPath.Substring("Metadata.".Length), out value) ? value : null;
            }

            switch (tokenPath)
            {
                case "TokenId":
                    return token.TokenId;
                case "ProductId":
                    return token.ProductId;
                case "WorkpieceId":
                    return token.WorkpieceId;
                case "PositionId":
                    return token.PositionId;
                case "CaptureGroupId":
                    return token.CaptureGroupId;
                case "ScanGroupId":
                    return token.ScanGroupId;
                case "FrameId":
                    return token.FrameId;
                case "CreatedAtUtc":
                    return token.CreatedAtUtc;
            }

            object tokenValue;
            if (token.TryGet(tokenPath, out tokenValue))
            {
                return tokenValue;
            }

            object metadataValue;
            return token.Metadata != null && token.Metadata.TryGetValue(tokenPath, out metadataValue) ? metadataValue : null;
        }
    }
}
