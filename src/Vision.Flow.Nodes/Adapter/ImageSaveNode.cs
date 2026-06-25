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
    // Image save nodes resolve images and path templates before calling the save adapter.
    public sealed class ImageSaveNodeConfig
    {
        public ImageSaveNodeConfig()
        {
            SaverId = "ImageSave01";
            FileNameTemplate = "{ImageId}.png";
            Queue = new AdapterNodeQueueConfig
            {
                QueueName = "image-save"
            };
        }

        public string SaverId { get; set; }

        public string ImageBinding { get; set; }

        public string ResultImageBinding { get; set; }

        public string RootDirectory { get; set; }

        public string DirectoryTemplate { get; set; }

        public string FileNameTemplate { get; set; }

        public AdapterNodeQueueConfig Queue { get; set; }
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
                FileNameTemplate = GetStringSetting(definition, "FileNameTemplate", "{ImageId}.png"),
                Queue = AdapterNodeHelpers.CreateQueueConfig(definition, "image-save")
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
            if (_config.Queue == null)
            {
                _config.Queue = new AdapterNodeQueueConfig { QueueName = "image-save" };
            }
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
            var imageStatus = ImageSaveWorkStatus.NotRequested;
            var resultImageStatus = ImageSaveWorkStatus.NotRequested;

            if (image != null)
            {
                imageStatus = await SaveOneAsync(context, saver, saverId, image, "Image", false, cancellationToken).ConfigureAwait(false);
                imagePath = imageStatus.Path;
            }

            if (resultImage != null)
            {
                resultImageStatus = await SaveOneAsync(context, saver, saverId, resultImage, "ResultImage", image != null, cancellationToken).ConfigureAwait(false);
                resultImagePath = resultImageStatus.Path;
            }

            context.Token.Set("ImagePath", imagePath);
            context.Token.Set("ResultImagePath", resultImagePath);

            return NodeExecutionResult.Success(
                "Next",
                new Dictionary<string, object>
                {
                    { "ImagePath", imagePath },
                    { "ResultImagePath", resultImagePath },
                    { "Queued", imageStatus.IsQueued || resultImageStatus.IsQueued },
                    { "QueueCompleted", imageStatus.IsCompleted && resultImageStatus.IsCompleted },
                    { "ImageQueued", imageStatus.IsQueued },
                    { "ResultImageQueued", resultImageStatus.IsQueued }
                });
        }

        private async Task<ImageSaveWorkStatus> SaveOneAsync(
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

            var queueResult = await AdapterNodeHelpers.ExecuteWithOptionalQueueResultAsync(
                context,
                _config.Queue,
                "image-save",
                "image.save." + role,
                async delegate(CancellationToken token)
                {
                    try
                    {
                        return await saver.SaveAsync(request, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (request.Image != null)
                        {
                            request.Image.Dispose();
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);

            if (!queueResult.WaitedForCompletion)
            {
                return new ImageSaveWorkStatus
                {
                    IsQueued = queueResult.IsQueued,
                    IsCompleted = false
                };
            }

            var result = queueResult.Value;
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

            return new ImageSaveWorkStatus
            {
                Path = result.Path,
                IsQueued = false,
                IsCompleted = true
            };
        }

        private sealed class ImageSaveWorkStatus
        {
            public static readonly ImageSaveWorkStatus NotRequested = new ImageSaveWorkStatus
            {
                IsCompleted = true
            };

            public string Path { get; set; }

            public bool IsQueued { get; set; }

            public bool IsCompleted { get; set; }
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
                    },
                    AdapterNodeDescriptors.QueueUseSetting(),
                    AdapterNodeDescriptors.QueueNameSetting("image-save"),
                    AdapterNodeDescriptors.QueueCapacitySetting(),
                    AdapterNodeDescriptors.QueueMaxDegreeSetting(),
                    AdapterNodeDescriptors.QueueFullModeSetting(),
                    AdapterNodeDescriptors.QueueWaitForCompletionSetting()
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
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Queued",
                        DisplayName = "Queued",
                        DataType = "Boolean",
                        Description = "True when one or more image save operations were accepted by a queue without waiting."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "QueueCompleted",
                        DisplayName = "Queue Completed",
                        DataType = "Boolean",
                        Description = "True when queued image save work completed before node output."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ImageQueued",
                        DisplayName = "Image Queued",
                        DataType = "Boolean",
                        Description = "True when the raw image save was queued without waiting."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ResultImageQueued",
                        DisplayName = "Result Image Queued",
                        DataType = "Boolean",
                        Description = "True when the result image save was queued without waiting."
                    }
                }
            };
        }
    }
}
