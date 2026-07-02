using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Services.Validation;

namespace Vision.Flow.Core.Services.Publishing
{
    public sealed class FlowPublishResult
    {
        public FlowPublishResult(RuntimeFlowDefinition runtime, FlowValidationResult validation)
        {
            Runtime = runtime;
            Validation = validation ?? new FlowValidationResult();
        }

        public RuntimeFlowDefinition Runtime { get; private set; }

        public FlowValidationResult Validation { get; private set; }

        public bool IsSuccess
        {
            get { return Validation.IsValid; }
        }
    }
}
