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
    public sealed class LightChannelControlConfig
    {
        public LightChannelControlConfig()
        {
            IsEnabled = true;
        }

        public string ChannelName { get; set; }

        public bool IsEnabled { get; set; }

        public double Intensity { get; set; }

        public int DurationMs { get; set; }
    }

    public sealed class LightControlNodeConfig
    {
        public LightControlNodeConfig()
        {
            Channels = new List<LightChannelControlConfig>();
        }

        public string LightId { get; set; }

        public IList<LightChannelControlConfig> Channels { get; set; }

        public int StableDelayMs { get; set; }
    }

    public sealed class LightControlNodeFactory : BaseNodeFactory<LightControlNodeConfig>
    {
        public const string TypeName = "light.control";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return LightControlNodeDescriptor.Create(); }
        }

        protected override LightControlNodeConfig CreateConfig(NodeDefinition definition)
        {
            var config = new LightControlNodeConfig
            {
                LightId = GetStringSetting(definition, "LightId", null),
                StableDelayMs = GetInt32Setting(definition, "StableDelayMs", 0)
            };

            AdapterNodeHelpers.AddLightChannels(config.Channels, GetSetting(definition, "Channels", null));

            var channelName = GetStringSetting(definition, "ChannelName", null);
            if (!string.IsNullOrWhiteSpace(channelName))
            {
                config.Channels.Add(new LightChannelControlConfig
                {
                    ChannelName = channelName,
                    IsEnabled = Convert.ToBoolean(GetSetting(definition, "IsEnabled", true), CultureInfo.InvariantCulture),
                    Intensity = Convert.ToDouble(GetSetting(definition, "Intensity", 0.0), CultureInfo.InvariantCulture),
                    DurationMs = GetInt32Setting(definition, "DurationMs", 0)
                });
            }

            return config;
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, LightControlNodeConfig config)
        {
            return new LightControlNode(config);
        }
    }

    public sealed class LightControlNode : IFlowNode
    {
        private readonly LightControlNodeConfig _config;

        public LightControlNode(LightControlNodeConfig config)
        {
            _config = config ?? new LightControlNodeConfig();
            if (_config.Channels == null)
            {
                _config.Channels = new List<LightChannelControlConfig>();
            }
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lightId = AdapterNodeHelpers.ResolveString(context, "LightId", _config.LightId);
            if (string.IsNullOrWhiteSpace(lightId))
            {
                return NodeExecutionResult.Failure("LightId is required.");
            }

            var stableDelayMs = AdapterNodeHelpers.ResolveInt32(context, "StableDelayMs", _config.StableDelayMs);
            if (stableDelayMs < 0)
            {
                return NodeExecutionResult.Failure("StableDelayMs must be greater than or equal to zero.");
            }

            var channels = ResolveChannels(context);
            if (channels.Count == 0)
            {
                return NodeExecutionResult.Failure("At least one light channel is required.");
            }

            var light = context.Devices.GetLight(lightId);
            var applied = new List<LightChannelSetting>();

            foreach (var channel in channels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (channel == null || string.IsNullOrWhiteSpace(channel.ChannelName))
                {
                    return NodeExecutionResult.Failure("Light channel name is required.");
                }

                var setting = new LightChannelSetting
                {
                    LightId = lightId,
                    ChannelName = channel.ChannelName,
                    IsEnabled = channel.IsEnabled,
                    Intensity = channel.Intensity,
                    DurationMs = channel.DurationMs
                };
                await light.SetAsync(setting, cancellationToken).ConfigureAwait(false);
                applied.Add(setting);
            }

            if (stableDelayMs > 0)
            {
                await Task.Delay(stableDelayMs, cancellationToken).ConfigureAwait(false);
            }

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "LightId", lightId },
                    { "ChannelCount", applied.Count },
                    { "StableDelayMs", stableDelayMs },
                    { "Channels", applied }
                });
        }

        private IList<LightChannelControlConfig> ResolveChannels(FlowExecutionContext context)
        {
            if (context.Node.InputBindings != null && context.Node.InputBindings.ContainsKey("Channels"))
            {
                var channels = new List<LightChannelControlConfig>();
                AdapterNodeHelpers.AddLightChannels(channels, context.GetInputValue("Channels"));
                return channels;
            }

            return _config.Channels;
        }
    }

    public static class LightControlNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = LightControlNodeFactory.TypeName,
                DisplayName = "Light Control",
                Category = "Device",
                Version = "1.0.0",
                Description = "Sets one or more light channels through a light adapter.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "LightId",
                        DisplayName = "Light",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered light adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "Channels",
                        DisplayName = "Channels",
                        DataType = "Object",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Channel settings with ChannelName, IsEnabled, Intensity, and DurationMs."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "StableDelayMs",
                        DisplayName = "Stable Delay (ms)",
                        DataType = "Int32",
                        DefaultValue = 0,
                        IsRequired = false,
                        Description = "Delay after applying light channels."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "LightId",
                        DisplayName = "Light",
                        DataType = "String",
                        Description = "The resolved light id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ChannelCount",
                        DisplayName = "Channel Count",
                        DataType = "Int32",
                        Description = "Number of applied channels."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "StableDelayMs",
                        DisplayName = "Stable Delay",
                        DataType = "Int32",
                        Description = "The resolved stable delay."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Channels",
                        DisplayName = "Channels",
                        DataType = "Object",
                        Description = "Applied channel settings."
                    }
                }
            };
        }
    }

    public sealed class RecipeRunNodeConfig
    {
        public RecipeRunNodeConfig()
        {
            TimeoutMs = 5000;
        }

        public string RecipeId { get; set; }

        public string InputImageBinding { get; set; }

        public int TimeoutMs { get; set; }
    }

    public sealed class RecipeRunNodeFactory : BaseNodeFactory<RecipeRunNodeConfig>
    {
        public const string TypeName = "recipe.run";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return RecipeRunNodeDescriptor.Create(); }
        }

        protected override RecipeRunNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new RecipeRunNodeConfig
            {
                RecipeId = GetStringSetting(definition, "RecipeId", null),
                InputImageBinding = GetStringSetting(definition, "InputImageBinding", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 5000)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, RecipeRunNodeConfig config)
        {
            return new RecipeRunNode(config);
        }
    }

    public sealed class RecipeRunNode : IFlowNode
    {
        private readonly RecipeRunNodeConfig _config;

        public RecipeRunNode(RecipeRunNodeConfig config)
        {
            _config = config ?? new RecipeRunNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipeId = AdapterNodeHelpers.ResolveString(context, "RecipeId", _config.RecipeId);
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return NodeExecutionResult.Failure("RecipeId is required.");
            }

            var timeoutMs = AdapterNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var recipe = context.Devices.GetRecipe(recipeId);
            var request = new RecipeRunRequest
            {
                RecipeId = recipeId,
                Token = context.Token
            };

            AddRecipeInputs(context, request);

            var stopwatch = Stopwatch.StartNew();
            RecipeRunResult result;
            using (var timeout = AdapterNodeTimeout.Create(timeoutMs, cancellationToken))
            {
                try
                {
                    result = await recipe.RunAsync(request, timeout.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    return NodeExecutionResult.Timeout("Timed out running recipe: " + recipeId, "Timeout");
                }
            }

            stopwatch.Stop();
            if (result == null)
            {
                return NodeExecutionResult.Failure("Recipe adapter returned a null result.");
            }

            var isOk = AdapterNodeHelpers.ResolveRecipeIsOk(result);
            var resultImage = AdapterNodeHelpers.ResolveResultImage(result);
            context.Token.Set("RecipeId", recipeId);
            context.Token.Set("RecipeStatus", result.Status);
            context.Token.Set("Result", string.IsNullOrWhiteSpace(result.Status) ? (isOk ? "OK" : "NG") : result.Status);
            context.Token.Set("IsOk", isOk);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "Result", result },
                    { "ResultImage", resultImage },
                    { "IsOk", isOk },
                    { "ElapsedMs", stopwatch.ElapsedMilliseconds }
                });
        }

        private void AddRecipeInputs(FlowExecutionContext context, RecipeRunRequest request)
        {
            var inputImage = AdapterNodeHelpers.ResolveConfiguredInput(context, "InputImage", _config.InputImageBinding);
            if (inputImage != null)
            {
                request.Inputs["InputImage"] = inputImage;
            }

            if (context.Node.InputBindings == null)
            {
                return;
            }

            foreach (var input in context.Node.InputBindings)
            {
                if (string.Equals(input.Key, "InputImage", StringComparison.OrdinalIgnoreCase) &&
                    request.Inputs.ContainsKey("InputImage"))
                {
                    continue;
                }

                request.Inputs[input.Key] = context.GetInputValue(input.Key);
            }
        }
    }

    public static class RecipeRunNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = RecipeRunNodeFactory.TypeName,
                DisplayName = "Run Recipe",
                Category = "Algorithm",
                Version = "1.0.0",
                Description = "Runs an inspection recipe through a recipe adapter.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.TimeoutOut("Routes recipe execution timeout."),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "RecipeId",
                        DisplayName = "Recipe",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered recipe adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "InputImageBinding",
                        DisplayName = "Input Image Binding",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Optional binding expression used when the InputImage input is not bound directly."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TimeoutMs",
                        DisplayName = "Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 5000,
                        IsRequired = false,
                        Description = "Maximum time for recipe execution. Zero disables the node timeout."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "Result",
                        DisplayName = "Result",
                        DataType = "RecipeRunResult",
                        Description = "Complete recipe adapter result."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ResultImage",
                        DisplayName = "Result Image",
                        DataType = "IVisionImage",
                        Description = "Optional result image produced by the recipe."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "IsOk",
                        DisplayName = "Is OK",
                        DataType = "Boolean",
                        Description = "Whether the recipe result is OK."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ElapsedMs",
                        DisplayName = "Elapsed (ms)",
                        DataType = "Int64",
                        Description = "Recipe execution elapsed time."
                    }
                }
            };
        }
    }

    public sealed class ImageSaveNodeConfig
    {
        public ImageSaveNodeConfig()
        {
            SaverId = "ImageSave01";
            FileNameTemplate = "{ImageId}.png";
        }

        public string SaverId { get; set; }

        public string ImageBinding { get; set; }

        public string ResultImageBinding { get; set; }

        public string RootDirectory { get; set; }

        public string DirectoryTemplate { get; set; }

        public string FileNameTemplate { get; set; }
    }

    public sealed class ImageSaveNodeFactory : BaseNodeFactory<ImageSaveNodeConfig>
    {
        public const string TypeName = "image.save";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return ImageSaveNodeDescriptor.Create(); }
        }

        protected override ImageSaveNodeConfig CreateConfig(NodeDefinition definition)
        {
            var saverId = GetStringSetting(definition, "SaverId", null);
            if (string.IsNullOrWhiteSpace(saverId))
            {
                saverId = GetStringSetting(definition, "ImageSaverId", "ImageSave01");
            }

            return new ImageSaveNodeConfig
            {
                SaverId = saverId,
                ImageBinding = GetStringSetting(definition, "ImageBinding", null),
                ResultImageBinding = GetStringSetting(definition, "ResultImageBinding", null),
                RootDirectory = GetStringSetting(definition, "RootDirectory", null),
                DirectoryTemplate = GetStringSetting(definition, "DirectoryTemplate", null),
                FileNameTemplate = GetStringSetting(definition, "FileNameTemplate", "{ImageId}.png")
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, ImageSaveNodeConfig config)
        {
            return new ImageSaveNode(config);
        }
    }

    public sealed class ImageSaveNode : IFlowNode
    {
        private readonly ImageSaveNodeConfig _config;

        public ImageSaveNode(ImageSaveNodeConfig config)
        {
            _config = config ?? new ImageSaveNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var saverId = AdapterNodeHelpers.ResolveString(context, "SaverId", _config.SaverId);
            if (string.IsNullOrWhiteSpace(saverId))
            {
                return NodeExecutionResult.Failure("SaverId is required.");
            }

            var image = AdapterNodeHelpers.ResolveVisionImage(
                AdapterNodeHelpers.ResolveConfiguredInput(context, "Image", _config.ImageBinding),
                "Image");
            var resultImage = AdapterNodeHelpers.ResolveVisionImage(
                AdapterNodeHelpers.ResolveConfiguredInput(context, "ResultImage", _config.ResultImageBinding),
                "ResultImage");

            if (image == null && resultImage == null)
            {
                return NodeExecutionResult.Failure("Image or ResultImage is required.");
            }

            var saver = context.Devices.GetImageSaver(saverId);
            var imagePath = default(string);
            var resultImagePath = default(string);

            if (image != null)
            {
                imagePath = await SaveOneAsync(context, saver, saverId, image, "Image", false, cancellationToken).ConfigureAwait(false);
            }

            if (resultImage != null)
            {
                resultImagePath = await SaveOneAsync(context, saver, saverId, resultImage, "ResultImage", image != null, cancellationToken).ConfigureAwait(false);
            }

            context.Token.Set("ImagePath", imagePath);
            context.Token.Set("ResultImagePath", resultImagePath);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "ImagePath", imagePath },
                    { "ResultImagePath", resultImagePath }
                });
        }

        private async Task<string> SaveOneAsync(
            FlowExecutionContext context,
            IImageSaveAdapter saver,
            string saverId,
            IVisionImage image,
            string role,
            bool appendResultSuffix,
            CancellationToken cancellationToken)
        {
            if (image.IsDisposed)
            {
                throw new InvalidOperationException("Cannot save a disposed image: " + image.ImageId);
            }

            var directory = AdapterNodeHelpers.RenderDirectoryTemplate(
                context,
                image,
                AdapterNodeHelpers.ResolveString(context, "RootDirectory", _config.RootDirectory),
                AdapterNodeHelpers.ResolveString(context, "DirectoryTemplate", _config.DirectoryTemplate));
            var fileName = AdapterNodeHelpers.RenderFileNameTemplate(
                context,
                image,
                AdapterNodeHelpers.ResolveString(context, "FileNameTemplate", _config.FileNameTemplate));
            if (appendResultSuffix)
            {
                fileName = AdapterNodeHelpers.AddFileNameSuffix(fileName, "_result");
            }

            var request = new ImageSaveRequest
            {
                SaverId = saverId,
                Image = image.CloneReference(),
                DirectoryPath = directory,
                FileName = fileName,
                Format = AdapterNodeHelpers.GetFileFormat(fileName)
            };
            request.Metadata["TokenId"] = context.Token.TokenId;
            request.Metadata["NodeId"] = context.Node.Id;
            request.Metadata["Role"] = role;

            var result = await saver.SaveAsync(request, cancellationToken).ConfigureAwait(false);
            if (result == null)
            {
                throw new InvalidOperationException("Image saver returned a null result.");
            }

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Message)
                    ? "Image saver reported failure."
                    : result.Message);
            }

            return result.Path;
        }
    }

    public static class ImageSaveNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = ImageSaveNodeFactory.TypeName,
                DisplayName = "Save Image",
                Category = "Storage",
                Version = "1.0.0",
                Description = "Saves raw and result images through an image save adapter.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "SaverId",
                        DisplayName = "Image Saver",
                        DataType = "String",
                        DefaultValue = "ImageSave01",
                        IsRequired = false,
                        Description = "Registered image save adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "ImageBinding",
                        DisplayName = "Image Binding",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Optional binding expression used when the Image input is not bound directly."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "ResultImageBinding",
                        DisplayName = "Result Image Binding",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Optional binding expression used when the ResultImage input is not bound directly."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "RootDirectory",
                        DisplayName = "Root Directory",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Root directory or adapter-specific base path."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "DirectoryTemplate",
                        DisplayName = "Directory Template",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Directory template. Supports tokens such as {Date:yyyyMMdd}, {CameraId}, {FrameId}, and {Result}."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "FileNameTemplate",
                        DisplayName = "File Name Template",
                        DataType = "String",
                        DefaultValue = "{ImageId}.png",
                        IsRequired = false,
                        Description = "File name template for saved images."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "ImagePath",
                        DisplayName = "Image Path",
                        DataType = "String",
                        Description = "Path returned for the raw image."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ResultImagePath",
                        DisplayName = "Result Image Path",
                        DataType = "String",
                        Description = "Path returned for the result image."
                    }
                }
            };
        }
    }

    public sealed class DatabaseFieldMappingConfig
    {
        public string FieldName { get; set; }

        public string InputName { get; set; }

        public object Value { get; set; }

        public string ValueBinding { get; set; }
    }

    public sealed class DatabaseSaveNodeConfig
    {
        public DatabaseSaveNodeConfig()
        {
            FieldMappings = new List<DatabaseFieldMappingConfig>();
        }

        public string DatabaseId { get; set; }

        public string TableName { get; set; }

        public IList<DatabaseFieldMappingConfig> FieldMappings { get; set; }
    }

    public sealed class DatabaseSaveNodeFactory : BaseNodeFactory<DatabaseSaveNodeConfig>
    {
        public const string TypeName = "database.save";

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return DatabaseSaveNodeDescriptor.Create(); }
        }

        protected override DatabaseSaveNodeConfig CreateConfig(NodeDefinition definition)
        {
            var config = new DatabaseSaveNodeConfig
            {
                DatabaseId = GetStringSetting(definition, "DatabaseId", null),
                TableName = GetStringSetting(definition, "TableName", null)
            };
            AdapterNodeHelpers.AddFieldMappings(config.FieldMappings, GetSetting(definition, "FieldMappings", null));
            return config;
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, DatabaseSaveNodeConfig config)
        {
            return new DatabaseSaveNode(config);
        }
    }

    public sealed class DatabaseSaveNode : IFlowNode
    {
        private readonly DatabaseSaveNodeConfig _config;

        public DatabaseSaveNode(DatabaseSaveNodeConfig config)
        {
            _config = config ?? new DatabaseSaveNodeConfig();
            if (_config.FieldMappings == null)
            {
                _config.FieldMappings = new List<DatabaseFieldMappingConfig>();
            }
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var databaseId = AdapterNodeHelpers.ResolveString(context, "DatabaseId", _config.DatabaseId);
            if (string.IsNullOrWhiteSpace(databaseId))
            {
                return NodeExecutionResult.Failure("DatabaseId is required.");
            }

            var tableName = AdapterNodeHelpers.ResolveString(context, "TableName", _config.TableName);
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return NodeExecutionResult.Failure("TableName is required.");
            }

            var values = CreateValues(context);
            var database = context.Devices.GetDatabase(databaseId);
            var request = new DatabaseSaveRequest
            {
                DatabaseId = databaseId,
                TableName = tableName,
                Values = values
            };
            AdapterNodeHelpers.AddTokenMetadata(context.Token, request.Metadata);
            request.Metadata["NodeId"] = context.Node.Id;

            await database.SaveAsync(request, cancellationToken).ConfigureAwait(false);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "Saved", true }
                });
        }

        private IDictionary<string, object> CreateValues(FlowExecutionContext context)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (_config.FieldMappings.Count > 0)
            {
                foreach (var mapping in _config.FieldMappings)
                {
                    if (mapping == null || string.IsNullOrWhiteSpace(mapping.FieldName))
                    {
                        continue;
                    }

                    values[mapping.FieldName] = AdapterNodeHelpers.ResolveFieldValue(context, mapping);
                }

                return values;
            }

            if (context.Node.InputBindings != null)
            {
                foreach (var input in context.Node.InputBindings)
                {
                    values[input.Key] = context.GetInputValue(input.Key);
                }
            }

            return values;
        }
    }

    public static class DatabaseSaveNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = DatabaseSaveNodeFactory.TypeName,
                DisplayName = "Save Database Row",
                Category = "Storage",
                Version = "1.0.0",
                Description = "Saves inspection values through a database adapter.",
                InputPorts =
                {
                    AdapterNodeDescriptors.ControlIn()
                },
                OutputPorts =
                {
                    AdapterNodeDescriptors.NextOut(),
                    AdapterNodeDescriptors.ErrorOut("Routes invalid configuration or adapter errors.")
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "DatabaseId",
                        DisplayName = "Database",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered database adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TableName",
                        DisplayName = "Table",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Target table name."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "FieldMappings",
                        DisplayName = "Field Mappings",
                        DataType = "Object",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Field mappings with FieldName and Value, ValueBinding, or InputName."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "Saved",
                        DisplayName = "Saved",
                        DataType = "Boolean",
                        Description = "True when the adapter save call completes."
                    }
                }
            };
        }
    }

    internal static class AdapterNodeDescriptors
    {
        public static NodePortDescriptor ControlIn()
        {
            return new NodePortDescriptor
            {
                Name = "In",
                DisplayName = "In",
                Direction = "Input",
                DataType = "Control",
                IsRequired = true,
                Description = "Execution input."
            };
        }

        public static NodePortDescriptor NextOut()
        {
            return new NodePortDescriptor
            {
                Name = "Next",
                DisplayName = "Next",
                Direction = "Output",
                DataType = "Control",
                Description = "Continues after successful execution."
            };
        }

        public static NodePortDescriptor ErrorOut(string description)
        {
            return new NodePortDescriptor
            {
                Name = "Error",
                DisplayName = "Error",
                Direction = "Output",
                DataType = "Control",
                Description = description
            };
        }

        public static NodePortDescriptor TimeoutOut(string description)
        {
            return new NodePortDescriptor
            {
                Name = "Timeout",
                DisplayName = "Timeout",
                Direction = "Output",
                DataType = "Control",
                Description = description
            };
        }
    }

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

    internal sealed class AdapterNodeTimeout : IDisposable
    {
        private readonly CancellationTokenSource _source;

        private AdapterNodeTimeout(CancellationTokenSource source)
        {
            _source = source;
        }

        public CancellationToken Token
        {
            get { return _source.Token; }
        }

        public static AdapterNodeTimeout Create(int timeoutMs, CancellationToken cancellationToken)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeoutMs > 0)
            {
                source.CancelAfter(timeoutMs);
            }

            return new AdapterNodeTimeout(source);
        }

        public void Dispose()
        {
            _source.Dispose();
        }
    }
}
