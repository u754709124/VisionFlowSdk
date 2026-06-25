using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // Fake recipes return deterministic algorithm results while preserving adapter request metadata.
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
            DelayMs = 0;
        }

        public string RecipeId { get; private set; }

        public int DelayMs { get; set; }

        public IDictionary<string, object> DefaultOutputs
        {
            get { return _defaultOutputs; }
        }

        public async Task<RecipeRunResult> RunAsync(RecipeRunRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken).ConfigureAwait(false);
            }

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

            return result;
        }
    }
}
