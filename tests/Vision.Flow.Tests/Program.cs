using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Nodes;
using Vision.Flow.Core.Domain.Nodes;
using Vision.Flow.Core.Runtime.Events;
using Vision.Flow.Core.Services.Serialization;
using Vision.Flow.Core.Services.Validation;
using Vision.Flow.Core.Domain.Flows;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Services.Publishing;
using Vision.Flow.Core.Contracts.Nodes;
using Vision.Flow.Core.Runtime.Engine;
using Vision.Flow.Core.Runtime.Execution;
using Vision.Flow.Core.Runtime.State;
using Vision.Flow.Designer.Wpf.Controls;
using Vision.Flow.Designer.Wpf.ViewModels;

namespace Vision.Flow.Tests
{
    // 娴嬭瘯妗嗘灦鍏ュ彛浠呬繚鐣欐敞鍐屽拰鎵ц缂栨帓銆?
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                return RunAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected test harness failure:");
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static async Task<int> RunAsync()
        {
            var tests = new List<TestCase>
            {
                new TestCase("FlowToken supports Set/Get/TryGet", FlowTokenTests.SetGetTryGet),
                new TestCase("Flow protocol constants keep existing wire values", FlowProtocolConstantsTests.ConstantsKeepExistingWireValues),
                new TestCase("Flow enum wire values keep existing strings", FlowProtocolConstantsTests.EnumWireValuesKeepExistingStrings),
                new TestCase("Non-camera contracts are not exposed", ApiSurfaceReductionTests.NonCameraContractsAreNotExposed),
                new TestCase("Queue runtime is not exposed", ApiSurfaceReductionTests.QueueRuntimeIsNotExposed),
                new TestCase("Removed camera frame router surface is not exposed", ApiSurfaceReductionTests.CameraFrameRouterSurfaceIsNotExposed),
                new TestCase("Domain constants do not expose removed names", ApiSurfaceReductionTests.DomainConstantsDoNotExposeRemovedNames),
                new TestCase("Source text files do not contain corrupted Chinese markers", SourceTextEncodingTests.TextFilesDoNotContainCorruptedChineseMarkers),
                new TestCase("Runtime serialization round-trips without view state", SerializationTests.RuntimeRoundTrip),
                new TestCase("Design serialization round-trips runtime and view state", SerializationTests.DesignRoundTrip),
                new TestCase("Legacy design serialization uses default canvas size", SerializationTests.DesignMissingCanvasSizeUsesDefaults),
                new TestCase("Runtime enum settings serialize as wire strings", SerializationTests.RuntimeEnumSettingsSerializeAsWireStrings),
                new TestCase("FlowValidator rejects duplicate NodeId", FlowValidationPublishTests.DuplicateNodeIdReturnsError),
                new TestCase("FlowValidator rejects dangling edges", FlowValidationPublishTests.DanglingEdgeReturnsError),
                new TestCase("FlowValidator rejects missing required settings", FlowValidationPublishTests.MissingRequiredSettingReturnsError),
                new TestCase("FlowValidator rejects missing binding outputs", FlowValidationPublishTests.MissingBindingOutputReturnsError),
                new TestCase("FlowValidator rejects invalid core node settings", FlowValidationPublishTests.InvalidCoreNodeSettingsReturnErrors),
                new TestCase("FlowPublishService removes designer view state", FlowValidationPublishTests.PublishRuntimeDoesNotContainViewState),
                new TestCase("FlowPublishService publishes a valid runtime", FlowValidationPublishTests.ValidFlowPublishesSuccessfully),
                new TestCase("Sample flow files deserialize and validate", SampleFlowTests.SampleFlowFilesDeserializeAndValidate),
                new TestCase("Sample runtime file excludes designer view state", SampleFlowTests.SampleRuntimeExcludesViewState),
                new TestCase("FlowRunner executes A -> B -> C and writes output variables", FlowRunnerTests.LinearOrderAndVariables),
                new TestCase("FlowRunner executes all fan-out edges from one output port", FlowRunnerTests.FanOutExecutesAllOutgoingEdges),
                new TestCase("FlowRunner executes fan-out branches in parallel when configured", FlowRunnerTests.ParallelFanOutExecutesBranchesInParallel),
                new TestCase("FlowRunner executes branched fan-out graph", FlowRunnerTests.BranchedFanOutGraphExecutesAllBranches),
                new TestCase("FlowRunner allows reconverging branches without global visited blocking", FlowRunnerTests.ReconvergingBranchesCanReachSameNode),
                new TestCase("FlowRunner publishes NodeFailed and follows Error route", FlowRunnerTests.NodeFailedAndErrorRoute),
                new TestCase("FlowRunner publishes NodeTimeout and follows Timeout route", FlowRunnerTests.NodeTimeoutAndTimeoutRoute),
                new TestCase("FlowRunner StopAsync cancels running flow", FlowRunnerTests.StopAsyncCancelsRunningFlow),
                new TestCase("FlowRunner continuation dispatcher routes output-port continuations", FlowRunnerTests.ContinuationDispatcherRoutesOutputPort),
                new TestCase("FlowRunner detects cycles on the current execution path", FlowRunnerTests.CycleRouteThrows),
                new TestCase("FlowRunner reports a clear missing entry exception", FlowRunnerTests.MissingEntryThrows),
                new TestCase("FlowRunner publishes runtime events in order", FlowRunnerTests.RuntimeEventOrder),
                new TestCase("VisionImageReference supports clone and disposal", CoreDeviceContractTests.VisionImageReferenceLifecycle),
                new TestCase("CommonNodeRegistration resolves common factories", CommonNodeTests.RegisterAllResolvesFactories),
                new TestCase("Common descriptors use strong enum types", CommonNodeTests.CommonDescriptorsUseStrongEnumTypes),
                new TestCase("LogNode publishes a runtime log event", CommonNodeTests.LogNodePublishesRuntimeEvent),
                new TestCase("LogNode accepts a strong enum level", CommonNodeTests.LogNodeAcceptsStrongEnumLevel),
                new TestCase("DelayNode executes a configured delay", CommonNodeTests.DelayNodeExecutes),
                new TestCase("VariableSetNode writes a variable subsequent node can read", CommonNodeTests.VariableSetNodeWritesVariableForNextNode),
                new TestCase("AndJoinNode triggers after two inputs with the same JoinKey", ControlFlowNodeTests.AndJoinTwoInputsSameJoinKey),
                new TestCase("AndJoinNode keeps different JoinKeys isolated", ControlFlowNodeTests.AndJoinDifferentKeysDoNotMix),
                new TestCase("AndJoinNode duplicate policy Error routes to Error", ControlFlowNodeTests.AndJoinDuplicatePolicyError),
                new TestCase("AndJoinNode accepts a strong duplicate policy", ControlFlowNodeTests.AndJoinAcceptsStrongDuplicatePolicy),
                new TestCase("ConditionNode routes true and false branches", ControlFlowNodeTests.ConditionTrueFalseRoutes),
                new TestCase("ConditionNode accepts a strong operator", ControlFlowNodeTests.ConditionAcceptsStrongOperator),
                new TestCase("Designer property panel read-only mode disables editors", DesignerInteractionTests.PropertyPanelReadOnlyDisablesEditors),
                new TestCase("Designer node palette read-only mode blocks node requests", DesignerInteractionTests.NodePaletteReadOnlyBlocksNodeRequests),
                new TestCase("Designer node palette single click selects only", DesignerInteractionTests.NodePaletteSingleClickSelectsOnly),
                new TestCase("Designer node palette double click requests node once", DesignerInteractionTests.NodePaletteDoubleClickRequestsNodeOnce),
                new TestCase("Designer node palette drag request carries descriptor", DesignerInteractionTests.NodePaletteDragRequestCarriesDescriptor),
                new TestCase("Designer stop marks running cards stopped", DesignerInteractionTests.StopMarksRunningCardsStopped),
                new TestCase("Designer debug buttons recover after stop", DesignerInteractionTests.DebugButtonsRecoverAfterStop),
                new TestCase("Designer embedded toolbar hides standalone document commands", DesignerInteractionTests.EmbeddedToolbarHidesStandaloneDocumentCommands),
                new TestCase("Designer host API loads and captures deep copies", DesignerInteractionTests.HostDocumentApiLoadsCapturesAndDeepCopies),
                new TestCase("Designer host API resets to an empty document", DesignerInteractionTests.HostResetCreatesEmptyDocument),
                new TestCase("Designer palette default add uses viewport center", DesignerInteractionTests.PaletteDefaultAddUsesViewportCenter),
                new TestCase("Designer node card uses sharp text rendering options", DesignerInteractionTests.NodeCardUsesSharpTextRenderingOptions),
                new TestCase("Designer node card shows runtime summary above card", DesignerInteractionTests.NodeCardShowsRuntimeSummaryAboveCard)
            };

            var failed = 0;
            foreach (var test in tests)
            {
                try
                {
                    await test.RunAsync().ConfigureAwait(false);
                    Console.WriteLine("[PASS] " + test.Name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("[FAIL] " + test.Name);
                    Console.WriteLine(ex);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Tests run: " + tests.Count + ", Failed: " + failed);
            return failed == 0 ? 0 : 1;
        }
    }
}
