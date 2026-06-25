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
    // Shared adapter node helpers centralize binding resolution, queue execution, and template rendering.
    internal static class AdapterNodeHelpers
    {
        private static readonly Regex TemplateTokenRegex = new Regex(@"\{(?<name>[A-Za-z0-9_.]+)(:(?<format>[^}]+))?\}", RegexOptions.Compiled);

        public static string ResolveString(FlowExecutionContext context, string name, string defaultValue)
        {
            var value = context.GetInputValue(name);
            return value == null ? defaultValue : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static int ResolveInt32(FlowExecutionContext context, string name, int defaultValue)
        {
            var value = context.GetInputValue(name);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public static bool ResolveBoolean(FlowExecutionContext context, string name, bool defaultValue)
        {
            var value = context.GetInputValue(name);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        public static AdapterNodeQueueConfig CreateQueueConfig(NodeDefinition definition, string defaultQueueName)
        {
            return new AdapterNodeQueueConfig
            {
                UseQueue = GetDefinitionBoolean(definition, "UseQueue", false),
                QueueName = GetDefinitionString(definition, "QueueName", defaultQueueName),
                QueueCapacity = GetDefinitionInt32(definition, "QueueCapacity", 16),
                QueueMaxDegreeOfParallelism = GetDefinitionInt32(definition, "QueueMaxDegreeOfParallelism", 1),
                QueueFullMode = GetDefinitionString(definition, "QueueFullMode", "Wait"),
                WaitForCompletion = GetDefinitionBoolean(definition, "WaitForCompletion", true)
            };
        }

        public static Task<T> ExecuteWithOptionalQueueAsync<T>(
            FlowExecutionContext context,
            AdapterNodeQueueConfig config,
            string defaultQueueName,
            string operationName,
            Func<CancellationToken, Task<T>> work,
            CancellationToken cancellationToken)
        {
            return ExecuteWithOptionalQueueAsync(context, config, defaultQueueName, operationName, work, true, cancellationToken);
        }

        public static async Task<T> ExecuteWithOptionalQueueAsync<T>(
            FlowExecutionContext context,
            AdapterNodeQueueConfig config,
            string defaultQueueName,
            string operationName,
            Func<CancellationToken, Task<T>> work,
            bool requireCompletion,
            CancellationToken cancellationToken)
        {
            var result = await ExecuteWithOptionalQueueResultAsync(
                context,
                config,
                defaultQueueName,
                operationName,
                work,
                cancellationToken).ConfigureAwait(false);

            if (!result.WaitedForCompletion && requireCompletion)
            {
                throw new InvalidOperationException("Queued adapter work was not configured to wait for completion: " + operationName);
            }

            return result.Value;
        }

        public static async Task<AdapterNodeQueueExecutionResult<T>> ExecuteWithOptionalQueueResultAsync<T>(
            FlowExecutionContext context,
            AdapterNodeQueueConfig config,
            string defaultQueueName,
            string operationName,
            Func<CancellationToken, Task<T>> work,
            CancellationToken cancellationToken)
        {
            var resolved = ResolveQueueConfig(context, config, defaultQueueName);
            if (!resolved.UseQueue)
            {
                return new AdapterNodeQueueExecutionResult<T>
                {
                    WaitedForCompletion = true,
                    Value = await work(cancellationToken).ConfigureAwait(false)
                };
            }

            if (!resolved.WaitForCompletion)
            {
                return await ExecuteQueuedDetachedCoreAsync(
                    context,
                    resolved,
                    operationName,
                    work,
                    cancellationToken).ConfigureAwait(false);
            }

            return await ExecuteQueuedCoreAsync(
                context,
                resolved,
                operationName,
                work,
                cancellationToken).ConfigureAwait(false);
        }

        public static async Task ExecuteWithOptionalQueueAsync(
            FlowExecutionContext context,
            AdapterNodeQueueConfig config,
            string defaultQueueName,
            string operationName,
            Func<CancellationToken, Task> work,
            CancellationToken cancellationToken)
        {
            await ExecuteWithOptionalQueueAsync<object>(
                context,
                config,
                defaultQueueName,
                operationName,
                async delegate(CancellationToken token)
                {
                    await work(token).ConfigureAwait(false);
                    return null;
                },
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<AdapterNodeQueueExecutionResult<T>> ExecuteQueuedCoreAsync<T>(
            FlowExecutionContext context,
            AdapterNodeQueueConfig config,
            string operationName,
            Func<CancellationToken, Task<T>> work,
            CancellationToken cancellationToken)
        {
            var queue = context.Queues.GetOrCreate(config.QueueName, CreateQueueOptions(config));
            var result = await queue.EnqueueAsync(
                work,
                CreateQueueItemContext(context, operationName),
                cancellationToken).ConfigureAwait(false);

            HandleQueueResult(result, true);

            return new AdapterNodeQueueExecutionResult<T>
            {
                WaitedForCompletion = true,
                IsQueued = result.IsAccepted,
                Value = result.Value
            };
        }

        private static async Task<AdapterNodeQueueExecutionResult<T>> ExecuteQueuedDetachedCoreAsync<T>(
            FlowExecutionContext context,
            AdapterNodeQueueConfig config,
            string operationName,
            Func<CancellationToken, Task<T>> work,
            CancellationToken cancellationToken)
        {
            var queue = context.Queues.GetOrCreate(config.QueueName, CreateQueueOptions(config));
            var result = await queue.EnqueueDetachedAsync(
                work,
                CreateQueueItemContext(context, operationName),
                cancellationToken).ConfigureAwait(false);

            HandleQueueResult(result, false);
            return new AdapterNodeQueueExecutionResult<T>
            {
                WaitedForCompletion = false,
                IsQueued = result.IsAccepted,
                IsDropped = result.IsDropped,
                IsNotifyOnly = result.IsNotifyOnly
            };
        }

        private static void HandleQueueResult(FlowTaskQueueResult result, bool requireSuccess)
        {
            if (result == null)
            {
                throw new InvalidOperationException("Queue returned a null result.");
            }

            if (result.ShouldStopFlow)
            {
                throw new OperationCanceledException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Queue requested flow stop."
                    : result.ErrorMessage);
            }

            if (result.IsRejected)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Queue rejected adapter work."
                    : result.ErrorMessage);
            }

            if ((result.IsDropped || result.IsNotifyOnly) && requireSuccess)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Queue did not execute adapter work."
                    : result.ErrorMessage);
            }

            if (requireSuccess && !result.IsSuccess)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Queued adapter work failed."
                    : result.ErrorMessage);
            }
        }

        private static AdapterNodeQueueConfig ResolveQueueConfig(FlowExecutionContext context, AdapterNodeQueueConfig config, string defaultQueueName)
        {
            config = config ?? new AdapterNodeQueueConfig { QueueName = defaultQueueName };
            var resolved = new AdapterNodeQueueConfig
            {
                UseQueue = ResolveBoolean(context, "UseQueue", config.UseQueue),
                QueueName = ResolveString(context, "QueueName", string.IsNullOrWhiteSpace(config.QueueName) ? defaultQueueName : config.QueueName),
                QueueCapacity = ResolveInt32(context, "QueueCapacity", config.QueueCapacity <= 0 ? 16 : config.QueueCapacity),
                QueueMaxDegreeOfParallelism = ResolveInt32(context, "QueueMaxDegreeOfParallelism", config.QueueMaxDegreeOfParallelism <= 0 ? 1 : config.QueueMaxDegreeOfParallelism),
                QueueFullMode = ResolveString(context, "QueueFullMode", string.IsNullOrWhiteSpace(config.QueueFullMode) ? "Wait" : config.QueueFullMode),
                WaitForCompletion = ResolveBoolean(context, "WaitForCompletion", config.WaitForCompletion)
            };

            if (string.IsNullOrWhiteSpace(resolved.QueueName))
            {
                resolved.QueueName = string.IsNullOrWhiteSpace(defaultQueueName) ? "default" : defaultQueueName;
            }

            if (resolved.QueueCapacity <= 0)
            {
                resolved.QueueCapacity = 1;
            }

            if (resolved.QueueMaxDegreeOfParallelism <= 0)
            {
                resolved.QueueMaxDegreeOfParallelism = 1;
            }

            return resolved;
        }

        private static FlowTaskQueueOptions CreateQueueOptions(AdapterNodeQueueConfig config)
        {
            return new FlowTaskQueueOptions
            {
                QueueName = config.QueueName,
                Capacity = config.QueueCapacity,
                MaxDegreeOfParallelism = config.QueueMaxDegreeOfParallelism,
                FullMode = ParseQueueFullMode(config.QueueFullMode)
            };
        }

        private static FlowTaskQueueFullMode ParseQueueFullMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return FlowTaskQueueFullMode.Wait;
            }

            FlowTaskQueueFullMode mode;
            if (Enum.TryParse(value, true, out mode))
            {
                return mode;
            }

            throw new InvalidOperationException("QueueFullMode must be Wait, Reject, Drop, StopFlow, or NotifyOnly.");
        }

        private static FlowTaskQueueItemContext CreateQueueItemContext(FlowExecutionContext context, string operationName)
        {
            var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            data["NodeType"] = context.Node.Type;
            return new FlowTaskQueueItemContext
            {
                FlowId = context.Flow.FlowId,
                TokenId = context.Token.TokenId,
                NodeId = context.Node.Id,
                NodeName = context.Node.Name,
                OperationName = operationName,
                Data = data
            };
        }

        private static string GetDefinitionString(NodeDefinition definition, string name, string defaultValue)
        {
            var value = GetDefinitionSetting(definition, name, defaultValue);
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetDefinitionInt32(NodeDefinition definition, string name, int defaultValue)
        {
            var value = GetDefinitionSetting(definition, name, defaultValue);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static bool GetDefinitionBoolean(NodeDefinition definition, string name, bool defaultValue)
        {
            var value = GetDefinitionSetting(definition, name, defaultValue);
            if (value == null)
            {
                return defaultValue;
            }

            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }

        private static object GetDefinitionSetting(NodeDefinition definition, string name, object defaultValue)
        {
            object value;
            if (definition != null && definition.Settings != null && definition.Settings.TryGetValue(name, out value))
            {
                return value;
            }

            return defaultValue;
        }

        public static object ResolveConfiguredInput(FlowExecutionContext context, string inputName, string bindingExpression)
        {
            if (HasInput(context, inputName))
            {
                return context.GetInputValue(inputName);
            }

            if (!string.IsNullOrWhiteSpace(bindingExpression))
            {
                return context.ResolveBinding(VariableBinding.ForExpression(bindingExpression));
            }

            return null;
        }

        public static IVisionImage ResolveVisionImage(object value, string name)
        {
            if (value == null)
            {
                return null;
            }

            var image = value as IVisionImage;
            if (image == null)
            {
                throw new InvalidCastException(name + " must be an IVisionImage.");
            }

            return image;
        }

        public static bool ResolveRecipeIsOk(RecipeRunResult result)
        {
            object isOkValue;
            if (result.Outputs != null && TryGetValue(result.Outputs, "IsOk", out isOkValue))
            {
                return Convert.ToBoolean(isOkValue, CultureInfo.InvariantCulture);
            }

            if (!result.IsSuccess)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(result.Status))
            {
                return true;
            }

            return string.Equals(result.Status, "OK", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result.Status, "Pass", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result.Status, "Passed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(result.Status, "Success", StringComparison.OrdinalIgnoreCase);
        }

        public static IVisionImage ResolveResultImage(RecipeRunResult result)
        {
            if (result == null || result.Outputs == null)
            {
                return null;
            }

            object value;
            if (TryGetValue(result.Outputs, "ResultImage", out value) ||
                TryGetValue(result.Outputs, "OutputImage", out value) ||
                TryGetValue(result.Outputs, "Image", out value))
            {
                return value as IVisionImage;
            }

            return null;
        }

        public static string RenderDirectoryTemplate(FlowExecutionContext context, IVisionImage image, string rootDirectory, string directoryTemplate)
        {
            var renderedDirectory = RenderTemplate(context, image, directoryTemplate);
            return CombinePath(rootDirectory, renderedDirectory);
        }

        public static string RenderFileNameTemplate(FlowExecutionContext context, IVisionImage image, string fileNameTemplate)
        {
            var template = string.IsNullOrWhiteSpace(fileNameTemplate) ? "{ImageId}.png" : fileNameTemplate;
            var fileName = RenderTemplate(context, image, template);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                var imageId = image == null || string.IsNullOrWhiteSpace(image.ImageId) ? Guid.NewGuid().ToString("N") : image.ImageId;
                return imageId + ".png";
            }

            return fileName;
        }

        public static string AddFileNameSuffix(string fileName, string suffix)
        {
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(suffix))
            {
                return fileName;
            }

            var slashIndex = Math.Max(fileName.LastIndexOf('/'), fileName.LastIndexOf('\\'));
            var dotIndex = fileName.LastIndexOf('.');
            if (dotIndex > slashIndex)
            {
                return fileName.Substring(0, dotIndex) + suffix + fileName.Substring(dotIndex);
            }

            return fileName + suffix;
        }

        public static string GetFileFormat(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "png";
            }

            var slashIndex = Math.Max(fileName.LastIndexOf('/'), fileName.LastIndexOf('\\'));
            var dotIndex = fileName.LastIndexOf('.');
            if (dotIndex > slashIndex && dotIndex < fileName.Length - 1)
            {
                return fileName.Substring(dotIndex + 1);
            }

            return "png";
        }

        public static void AddTokenMetadata(FlowToken token, IDictionary<string, object> metadata)
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
        }

        public static object ResolveFieldValue(FlowExecutionContext context, DatabaseFieldMappingConfig mapping)
        {
            if (!string.IsNullOrWhiteSpace(mapping.InputName))
            {
                return context.GetInputValue(mapping.InputName);
            }

            if (!string.IsNullOrWhiteSpace(mapping.ValueBinding))
            {
                return context.ResolveBinding(VariableBinding.ForExpression(mapping.ValueBinding));
            }

            if (context.Node.InputBindings != null && context.Node.InputBindings.ContainsKey(mapping.FieldName))
            {
                return context.GetInputValue(mapping.FieldName);
            }

            return mapping.Value;
        }

        public static void AddLightChannels(IList<LightChannelControlConfig> channels, object value)
        {
            if (channels == null || value == null)
            {
                return;
            }

            var typed = value as LightChannelControlConfig;
            if (typed != null)
            {
                channels.Add(CloneChannel(typed));
                return;
            }

            var setting = value as LightChannelSetting;
            if (setting != null)
            {
                channels.Add(new LightChannelControlConfig
                {
                    ChannelName = setting.ChannelName,
                    IsEnabled = setting.IsEnabled,
                    Intensity = setting.Intensity,
                    DurationMs = setting.DurationMs
                });
                return;
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                if (LooksLikeLightChannel(dictionary))
                {
                    channels.Add(CreateChannelFromDictionary(dictionary));
                    return;
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    channels.Add(new LightChannelControlConfig
                    {
                        ChannelName = Convert.ToString(entry.Key, CultureInfo.InvariantCulture),
                        IsEnabled = true,
                        Intensity = Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture)
                    });
                }

                return;
            }

            if (value is string)
            {
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (var item in enumerable)
            {
                AddLightChannels(channels, item);
            }
        }

        public static void AddFieldMappings(IList<DatabaseFieldMappingConfig> mappings, object value)
        {
            if (mappings == null || value == null)
            {
                return;
            }

            var typed = value as DatabaseFieldMappingConfig;
            if (typed != null)
            {
                mappings.Add(CloneFieldMapping(typed));
                return;
            }

            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                if (LooksLikeFieldMapping(dictionary))
                {
                    mappings.Add(CreateFieldMappingFromDictionary(dictionary));
                    return;
                }

                foreach (DictionaryEntry entry in dictionary)
                {
                    var mapping = new DatabaseFieldMappingConfig
                    {
                        FieldName = Convert.ToString(entry.Key, CultureInfo.InvariantCulture)
                    };
                    var text = entry.Value == null ? null : Convert.ToString(entry.Value, CultureInfo.InvariantCulture);
                    if (!string.IsNullOrWhiteSpace(text) && text.Trim().StartsWith("{{", StringComparison.Ordinal))
                    {
                        mapping.ValueBinding = text;
                    }
                    else
                    {
                        mapping.Value = entry.Value;
                    }

                    mappings.Add(mapping);
                }

                return;
            }

            if (value is string)
            {
                return;
            }

            var enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (var item in enumerable)
            {
                AddFieldMappings(mappings, item);
            }
        }

        private static string RenderTemplate(FlowExecutionContext context, IVisionImage image, string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            var values = CreateTemplateValues(context, image);
            var now = DateTime.Now;
            return TemplateTokenRegex.Replace(
                template,
                delegate(Match match)
                {
                    var name = match.Groups["name"].Value;
                    var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;

                    if (string.Equals(name, "Date", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Time", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Now", StringComparison.OrdinalIgnoreCase))
                    {
                        return FormatValue(now, format);
                    }

                    object value;
                    if (values.TryGetValue(name, out value))
                    {
                        return FormatValue(value, format);
                    }

                    return string.Empty;
                });
        }

        private static IDictionary<string, object> CreateTemplateValues(FlowExecutionContext context, IVisionImage image)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (context != null && context.Token != null)
            {
                values["TokenId"] = context.Token.TokenId;
                values["ProductId"] = context.Token.ProductId;
                values["WorkpieceId"] = context.Token.WorkpieceId;
                values["PositionId"] = context.Token.PositionId;
                values["CaptureGroupId"] = context.Token.CaptureGroupId;
                values["ScanGroupId"] = context.Token.ScanGroupId;
                values["FrameId"] = context.Token.FrameId;

                CopyValues(context.Token.Metadata, values, false);
                CopyValues(context.Token.Values, values, true);
            }

            if (image != null)
            {
                values["ImageId"] = image.ImageId;
                values["Width"] = image.Width;
                values["Height"] = image.Height;
                values["PixelFormat"] = image.PixelFormat;
                values["CreatedUtc"] = image.CreatedUtc;
                CopyValues(image.Metadata, values, false);
            }

            return values;
        }

        private static void CopyValues(IDictionary<string, object> source, IDictionary<string, object> target, bool overwrite)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (var item in source)
            {
                if (overwrite || !target.ContainsKey(item.Key))
                {
                    target[item.Key] = item.Value;
                }
            }
        }

        private static string FormatValue(object value, string format)
        {
            if (value == null)
            {
                return string.Empty;
            }

            var formattable = value as IFormattable;
            if (formattable != null && !string.IsNullOrWhiteSpace(format))
            {
                return formattable.ToString(format, CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string CombinePath(string root, string child)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return child;
            }

            if (string.IsNullOrWhiteSpace(child))
            {
                return root;
            }

            if (root.EndsWith("/", StringComparison.Ordinal) || root.EndsWith("\\", StringComparison.Ordinal))
            {
                return root + child;
            }

            return root + "/" + child;
        }

        private static bool HasInput(FlowExecutionContext context, string inputName)
        {
            if (context == null || context.Node == null || string.IsNullOrWhiteSpace(inputName))
            {
                return false;
            }

            if (context.Node.InputBindings != null && context.Node.InputBindings.ContainsKey(inputName))
            {
                return true;
            }

            return context.Node.Settings != null && context.Node.Settings.ContainsKey(inputName);
        }

        private static bool LooksLikeLightChannel(IDictionary dictionary)
        {
            return ContainsKey(dictionary, "ChannelName") ||
                ContainsKey(dictionary, "Name") ||
                ContainsKey(dictionary, "Channel") ||
                ContainsKey(dictionary, "IsEnabled") ||
                ContainsKey(dictionary, "Enabled") ||
                ContainsKey(dictionary, "Intensity") ||
                ContainsKey(dictionary, "DurationMs");
        }

        private static LightChannelControlConfig CreateChannelFromDictionary(IDictionary dictionary)
        {
            object name;
            if (!TryGetValue(dictionary, "ChannelName", out name) &&
                !TryGetValue(dictionary, "Name", out name))
            {
                TryGetValue(dictionary, "Channel", out name);
            }

            object enabled;
            if (!TryGetValue(dictionary, "IsEnabled", out enabled))
            {
                TryGetValue(dictionary, "Enabled", out enabled);
            }

            object intensity;
            TryGetValue(dictionary, "Intensity", out intensity);

            object durationMs;
            TryGetValue(dictionary, "DurationMs", out durationMs);

            return new LightChannelControlConfig
            {
                ChannelName = name == null ? null : Convert.ToString(name, CultureInfo.InvariantCulture),
                IsEnabled = enabled == null || Convert.ToBoolean(enabled, CultureInfo.InvariantCulture),
                Intensity = intensity == null ? 0.0 : Convert.ToDouble(intensity, CultureInfo.InvariantCulture),
                DurationMs = durationMs == null ? 0 : Convert.ToInt32(durationMs, CultureInfo.InvariantCulture)
            };
        }

        private static LightChannelControlConfig CloneChannel(LightChannelControlConfig channel)
        {
            return new LightChannelControlConfig
            {
                ChannelName = channel.ChannelName,
                IsEnabled = channel.IsEnabled,
                Intensity = channel.Intensity,
                DurationMs = channel.DurationMs
            };
        }

        private static bool LooksLikeFieldMapping(IDictionary dictionary)
        {
            return ContainsKey(dictionary, "FieldName") ||
                ContainsKey(dictionary, "Name") ||
                ContainsKey(dictionary, "Field") ||
                ContainsKey(dictionary, "Column") ||
                ContainsKey(dictionary, "InputName") ||
                ContainsKey(dictionary, "Input") ||
                ContainsKey(dictionary, "ValueBinding") ||
                ContainsKey(dictionary, "Binding") ||
                ContainsKey(dictionary, "Expression") ||
                ContainsKey(dictionary, "Value") ||
                ContainsKey(dictionary, "ConstantValue");
        }

        private static DatabaseFieldMappingConfig CreateFieldMappingFromDictionary(IDictionary dictionary)
        {
            object fieldName;
            if (!TryGetValue(dictionary, "FieldName", out fieldName) &&
                !TryGetValue(dictionary, "Name", out fieldName) &&
                !TryGetValue(dictionary, "Field", out fieldName))
            {
                TryGetValue(dictionary, "Column", out fieldName);
            }

            object inputName;
            if (!TryGetValue(dictionary, "InputName", out inputName))
            {
                TryGetValue(dictionary, "Input", out inputName);
            }

            object valueBinding;
            if (!TryGetValue(dictionary, "ValueBinding", out valueBinding) &&
                !TryGetValue(dictionary, "Binding", out valueBinding))
            {
                TryGetValue(dictionary, "Expression", out valueBinding);
            }

            object value;
            if (!TryGetValue(dictionary, "Value", out value))
            {
                TryGetValue(dictionary, "ConstantValue", out value);
            }

            return new DatabaseFieldMappingConfig
            {
                FieldName = fieldName == null ? null : Convert.ToString(fieldName, CultureInfo.InvariantCulture),
                InputName = inputName == null ? null : Convert.ToString(inputName, CultureInfo.InvariantCulture),
                ValueBinding = valueBinding == null ? null : Convert.ToString(valueBinding, CultureInfo.InvariantCulture),
                Value = value
            };
        }

        private static DatabaseFieldMappingConfig CloneFieldMapping(DatabaseFieldMappingConfig mapping)
        {
            return new DatabaseFieldMappingConfig
            {
                FieldName = mapping.FieldName,
                InputName = mapping.InputName,
                Value = mapping.Value,
                ValueBinding = mapping.ValueBinding
            };
        }

        private static bool ContainsKey(IDictionary dictionary, string key)
        {
            object value;
            return TryGetValue(dictionary, key, out value);
        }

        private static bool TryGetValue(IDictionary<string, object> dictionary, string key, out object value)
        {
            value = null;
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (var entry in dictionary)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetValue(IDictionary dictionary, string key, out object value)
        {
            value = null;
            if (dictionary == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            foreach (DictionaryEntry entry in dictionary)
            {
                if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase))
                {
                    value = entry.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
