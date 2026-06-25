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
    // Recipe nodes marshal token and image inputs into recipe adapter requests.
    public sealed class RecipeRunNodeConfig
    {
        public RecipeRunNodeConfig()
        {
            TimeoutMs = 5000;
            Queue = new AdapterNodeQueueConfig
            {
                QueueName = "recipe"
            };
        }

        public string RecipeId { get; set; }

        public string InputImageBinding { get; set; }

        public int TimeoutMs { get; set; }

        public AdapterNodeQueueConfig Queue { get; set; }
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
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 5000),
                Queue = AdapterNodeHelpers.CreateQueueConfig(definition, "recipe")
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
            if (_config.Queue == null)
            {
                _config.Queue = new AdapterNodeQueueConfig { QueueName = "recipe" };
            }
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
            AdapterNodeQueueExecutionResult<RecipeRunResult> queueResult;
            try
            {
                queueResult = await AdapterNodeHelpers.ExecuteWithOptionalQueueResultAsync(
                    context,
                    _config.Queue,
                    "recipe",
                    "recipe.run",
                    delegate(CancellationToken token)
                    {
                        return RunRecipeWithTimeoutAsync(recipe, request, timeoutMs, token);
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return NodeExecutionResult.Timeout("Timed out running recipe: " + recipeId, "Timeout");
            }

            stopwatch.Stop();
            if (!queueResult.WaitedForCompletion)
            {
                context.Token.Set("RecipeId", recipeId);
                context.Token.Set("RecipeQueued", queueResult.IsQueued);
                return NodeExecutionResult.Success(
                    "Next",
                    new Dictionary<string, object>
                    {
                        { "Result", null },
                        { "ResultImage", null },
                        { "IsOk", false },
                        { "ElapsedMs", stopwatch.ElapsedMilliseconds },
                        { "Queued", queueResult.IsQueued },
                        { "QueueCompleted", false },
                        { "QueueDropped", queueResult.IsDropped },
                        { "QueueNotifyOnly", queueResult.IsNotifyOnly }
                    });
            }

            var result = queueResult.Value;
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
                    { "ElapsedMs", stopwatch.ElapsedMilliseconds },
                    { "Queued", false },
                    { "QueueCompleted", true }
                });
        }

        private static async Task<RecipeRunResult> RunRecipeWithTimeoutAsync(
            IRecipeAdapter recipe,
            RecipeRunRequest request,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            using (var timeout = AdapterNodeTimeout.Create(timeoutMs, cancellationToken))
            {
                return await recipe.RunAsync(request, timeout.Token).ConfigureAwait(false);
            }
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
                    },
                    AdapterNodeDescriptors.QueueUseSetting(),
                    AdapterNodeDescriptors.QueueNameSetting("recipe"),
                    AdapterNodeDescriptors.QueueCapacitySetting(),
                    AdapterNodeDescriptors.QueueMaxDegreeSetting(),
                    AdapterNodeDescriptors.QueueFullModeSetting(),
                    AdapterNodeDescriptors.QueueWaitForCompletionSetting()
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
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Queued",
                        DisplayName = "Queued",
                        DataType = "Boolean",
                        Description = "True when recipe work was accepted by a queue without waiting."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "QueueCompleted",
                        DisplayName = "Queue Completed",
                        DataType = "Boolean",
                        Description = "True when queued recipe work completed before node output."
                    }
                }
            };
        }
    }
}
