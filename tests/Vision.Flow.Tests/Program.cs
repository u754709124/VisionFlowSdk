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
    // 测试框架入口仅保留注册和执行编排。
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
                new TestCase("Device contract surface is constrained", ApiSurfaceReductionTests.DeviceContractSurfaceIsConstrained),
                new TestCase("Queue runtime is not exposed", ApiSurfaceReductionTests.QueueRuntimeIsNotExposed),
                new TestCase("Removed camera frame router surface is not exposed", ApiSurfaceReductionTests.CameraFrameRouterSurfaceIsNotExposed),
                new TestCase("Domain constants do not expose removed names", ApiSurfaceReductionTests.DomainConstantsDoNotExposeRemovedNames),
                new TestCase("Source text files do not contain corrupted Chinese markers", SourceTextEncodingTests.TextFilesDoNotContainCorruptedChineseMarkers),
                new TestCase("Runtime serialization round-trips without view state", SerializationTests.RuntimeRoundTrip),
                new TestCase("Design serialization round-trips runtime and view state", SerializationTests.DesignRoundTrip),
                new TestCase("Schema v1 design files are rejected", SerializationTests.DesignV1IsRejected),
                new TestCase("Schema v1 runtime files are rejected", SerializationTests.RuntimeV1IsRejected),
                new TestCase("Runtime enum settings serialize as wire strings", SerializationTests.RuntimeEnumSettingsSerializeAsWireStrings),
                new TestCase("Setting resolver handles constant, node output and token paths", SettingValueResolverTests.ResolvesConstantNodeOutputAndTokenPaths),
                new TestCase("Flow data type compatibility follows variable setting rules", SettingValueResolverTests.DataTypeCompatibilityRules),
                new TestCase("Typed Object selectors validate roots and first-level members", DynamicDescriptorTests.TypedObjectSelectorsValidateRootsAndFirstLayerMembers),
                new TestCase("Environment variables serialize and publish with runtime flows", EnvironmentVariableTests.SerializationAndPublishPreserveDefinitions),
                new TestCase("Environment variables resolve defaults and runtime overrides", EnvironmentVariableTests.RuntimeValuesUseDefaultsAndOverrides),
                new TestCase("Environment variable definitions and references are validated", EnvironmentVariableTests.ValidatorRejectsInvalidDefinitionsAndReferences),
                new TestCase("Designer exposes environment variable suggestions", EnvironmentVariableTests.DesignerSuggestionsExposeEnvironmentVariables),
                new TestCase("Global variables serialize with defaults and selectors", GlobalVariableTests.SerializationPreservesDefinitionsAndSelectors),
                new TestCase("Global variable stores are typed isolated and reset per runner", GlobalVariableTests.StoreIsTypedAtomicIsolatedAndResetPerRunner),
                new TestCase("Global variable definitions and selectors are validated", GlobalVariableTests.ValidatorRejectsInvalidDefinitionsAndReferences),
                new TestCase("Variable set writes global constants and upstream outputs", GlobalVariableTests.VariableSetWritesConstantsAndUpstreamOutputs),
                new TestCase("Variable set descriptor follows the target global type", GlobalVariableTests.FlowAwareDescriptorTracksTargetTypeAndDeletion),
                new TestCase("Designer exposes globals and ordered mapping editor", GlobalVariableTests.DesignerExposesGlobalsAndOrderedMappingEditor),
                new TestCase("Nested mapping selectors are validated for publication", GlobalVariableTests.MappingSelectorsUseNestedPublishValidation),
                new TestCase("FlowValidator rejects duplicate NodeId", FlowValidationPublishTests.DuplicateNodeIdReturnsError),
                new TestCase("FlowValidator rejects dangling edges", FlowValidationPublishTests.DanglingEdgeReturnsError),
                new TestCase("FlowValidator rejects missing required settings", FlowValidationPublishTests.MissingRequiredSettingReturnsError),
                new TestCase("FlowValidator rejects missing variable outputs", FlowValidationPublishTests.MissingVariableOutputReturnsError),
                new TestCase("FlowValidator warns when an entry bypasses a variable source", FlowValidationPublishTests.EntryBypassProducesVariableAvailabilityWarning),
                new TestCase("FlowValidator rejects invalid core node settings", FlowValidationPublishTests.InvalidCoreNodeSettingsReturnErrors),
                new TestCase("FlowValidator applies typed custom setting validators", FlowValidationPublishTests.CustomSettingValidatorsAreApplied),
                new TestCase("NodeRegistry resolves static and instance descriptors", DynamicDescriptorTests.RegistryResolvesStaticAndInstanceDescriptors),
                new TestCase("FlowValidator uses instance descriptor contracts", DynamicDescriptorTests.ValidatorUsesInstanceDescriptorContracts),
                new TestCase("FlowValidator reports descriptor resolution failures", DynamicDescriptorTests.ValidatorReportsDescriptorResolutionFailures),
                new TestCase("Dynamic descriptor settings keep schema v2 serialization", DynamicDescriptorTests.DynamicDescriptorSettingsKeepSchemaV2Serialization),
                new TestCase("FlowPublishService removes designer view state", FlowValidationPublishTests.PublishRuntimeDoesNotContainViewState),
                new TestCase("FlowPublishService publishes a valid runtime", FlowValidationPublishTests.ValidFlowPublishesSuccessfully),
                new TestCase("FlowPublishService rejects v1 design and runtime schemas", FlowValidationPublishTests.PublishRejectsV1Schemas),
                new TestCase("FlowPublishService writes a validated runtime file", FlowValidationPublishTests.PublishToFileWritesValidatedRuntimeSnapshot),
                new TestCase("Invalid publication does not overwrite runtime file", FlowValidationPublishTests.InvalidPublishDoesNotOverwriteRuntimeFile),
                new TestCase("Runtime publication requires .flowruntime extension", FlowValidationPublishTests.PublishToFileRequiresRuntimeExtension),
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
                new TestCase("Concurrent StopAsync drains active FlowRuns exactly once", FlowRunnerTests.ConcurrentStopDrainsActiveRunExactlyOnce),
                new TestCase("Terminal sink failure does not duplicate FlowRun terminal", FlowRunnerTests.TerminalSinkFailureDoesNotDuplicateTerminal),
                new TestCase("FlowRunner continuation dispatcher routes output-port continuations", FlowRunnerTests.ContinuationDispatcherRoutesOutputPort),
                new TestCase("FlowRunner detects cycles on the current execution path", FlowRunnerTests.CycleRouteThrows),
                new TestCase("Late continuation publishes a rejected FlowRun terminal", FlowRunnerTests.ContinuationAfterStopPublishesRejectedTerminal),
                new TestCase("FlowRunner reports a clear missing entry exception", FlowRunnerTests.MissingEntryThrows),
                new TestCase("FlowRunner publishes runtime events in order", FlowRunnerTests.RuntimeEventOrder),
                new TestCase("In-memory sink preserves parameterless binary contract", FlowEventSinkTests.ParameterlessInMemorySinkPreservesBinaryContract),
                new TestCase("Runtime event snapshots remove resource references", FlowEventSinkTests.SanitizerRemovesResourceReferences),
                new TestCase("Bounded event sink contains telemetry pressure", FlowEventSinkTests.BoundedSinkContainsTelemetryPressure),
                new TestCase("Node retry is disabled by default", NodeExecutionPolicyTests.RetryDisabledExecutesOnce),
                new TestCase("Node retry count and interval are applied", NodeExecutionPolicyTests.RetryCountAndIntervalAreApplied),
                new TestCase("Node retry can recover execution", NodeExecutionPolicyTests.RetryCanRecoverNode),
                new TestCase("Node failure defaults to StopFlow", NodeExecutionPolicyTests.StopFlowIsTheDefaultFailureStrategy),
                new TestCase("Node ErrorBranch continues through a connected port", NodeExecutionPolicyTests.ErrorBranchContinuesThroughConnectedPort),
                new TestCase("Node DefaultOutputs continues through Next", NodeExecutionPolicyTests.DefaultOutputsContinueThroughNext),
                new TestCase("Node timeout participates in retry", NodeExecutionPolicyTests.TimeoutParticipatesInRetry),
                new TestCase("Binding and configuration failures do not retry", NodeExecutionPolicyTests.BindingAndConfigurationFailuresDoNotRetry),
                new TestCase("Cancellation interrupts node retry interval", NodeExecutionPolicyTests.CancellationInterruptsRetryInterval),
                new TestCase("Sequential ready queue preserves edge definition order", ReadyQueueSchedulingTests.SequentialReadyQueuePreservesDefinitionOrder),
                new TestCase("Parallel ready queue overlaps fan-out branches", ReadyQueueSchedulingTests.ParallelFanOutRunsBranchesConcurrently),
                new TestCase("Parallel StopFlow cancels sibling branches", ReadyQueueSchedulingTests.ParallelStopFlowCancelsSiblingBranch),
                new TestCase("Fan-in executes once after all inbound edges resolve", ReadyQueueSchedulingTests.FanInExecutesOnceAfterAllInboundEdgesResolve),
                new TestCase("Unselected condition branch propagates skip", ReadyQueueSchedulingTests.UnselectedConditionalBranchPropagatesSkip),
                new TestCase("All-skipped fan-in does not execute", ReadyQueueSchedulingTests.AllInboundEdgesSkippedDoesNotExecuteNode),
                new TestCase("Entry can start from a middle node", ReadyQueueSchedulingTests.EntryCanStartFromMiddleNode),
                new TestCase("NodeEvent continuation uses ready queue", ReadyQueueSchedulingTests.NodeEventContinuationUsesReadyQueue),
                new TestCase("Node concurrency defaults to one across runs", NodeConcurrencyGateTests.DefaultLimitSerializesSameNodeAcrossRuns),
                new TestCase("Node concurrency limit allows two runs", NodeConcurrencyGateTests.ConfiguredLimitAllowsTwoSameNodeRuns),
                new TestCase("Different nodes use independent concurrency gates", NodeConcurrencyGateTests.DifferentNodesDoNotShareGate),
                new TestCase("Node gate waiting can be cancelled", NodeConcurrencyGateTests.WaitingForGateCanBeCancelled),
                new TestCase("Node retry interval keeps concurrency lease", NodeConcurrencyGateTests.RetryIntervalKeepsGateLease),
                new TestCase("Timed-out attempts drain before retry", NodeConcurrencyGateTests.TimedOutAttemptDrainsBeforeRetry),
                new TestCase("External triggers validate and resolve declared inputs", FlowTriggerTests.ExternalTriggerUsesDeclaredInputs),
                new TestCase("Entry queue rejects concurrent runs when full", FlowTriggerTests.EntryQueueRejectsWhenFull),
                new TestCase("Entry concurrency policy controls serial and parallel runs", FlowTriggerTests.EntryConcurrencyPolicyControlsSerialAndParallelRuns),
                new TestCase("NodeEvent starts only its referenced listener", FlowTriggerTests.NodeEventStartsOnlyReferencedListener),
                new TestCase("TriggerInput selectors are validated against reachable entries", FlowTriggerTests.TriggerInputSelectorsAreValidated),
                new TestCase("VisionImageReference supports clone and disposal", CoreDeviceContractTests.VisionImageReferenceLifecycle),
                new TestCase("Motion adapter models use read-only snapshots", CoreDeviceContractTests.MotionAdapterModelsUseReadOnlySnapshots),
                new TestCase("Light controller registry uses explicit contract", CoreDeviceContractTests.LightControllerRegistryUsesExplicitContract),
                new TestCase("CommonNodeRegistration resolves common factories", CommonNodeTests.RegisterAllResolvesFactories),
                new TestCase("Common descriptors use strong enum types", CommonNodeTests.CommonDescriptorsUseStrongEnumTypes),
                new TestCase("Common descriptors use Chinese node metadata", CommonNodeTests.CommonDescriptorsUseChineseNodeMetadata),
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
                new TestCase("Designer property panel applies custom constant validation", DesignerInteractionTests.PropertyPanelAppliesCustomConstantValidation),
                new TestCase("Designer property panel read-only mode disables editors", DesignerInteractionTests.PropertyPanelReadOnlyDisablesEditors),
                new TestCase("Designer property panel uses host provided constant options", DesignerInteractionTests.PropertyPanelUsesHostProvidedConstantOptions),
                new TestCase("Designer property panel uses enum constants and exact enum variables", DesignerInteractionTests.PropertyPanelUsesEnumConstantsAndExactEnumVariables),
                new TestCase("Designer typed Object menus select roots and first-level members", DesignerInteractionTests.TypedObjectMenuSelectsRootAndFirstLayerMembers),
                new TestCase("Designer property panel uses modern editor types and separated segments", DesignerInteractionTests.PropertyPanelUsesModernEditorTypesAndSeparatedSegments),
                new TestCase("Designer property text editors keep single and multiline layout rules", DesignerInteractionTests.PropertyTextEditorsKeepSingleAndMultilineLayoutRules),
                new TestCase("Designer property validation slots keep editor positions stable", DesignerInteractionTests.PropertyValidationSlotsKeepEditorPositionsStable),
                new TestCase("Designer node palette read-only mode blocks node requests", DesignerInteractionTests.NodePaletteReadOnlyBlocksNodeRequests),
                new TestCase("Designer node palette single click selects only", DesignerInteractionTests.NodePaletteSingleClickSelectsOnly),
                new TestCase("Designer node palette double click requests node once", DesignerInteractionTests.NodePaletteDoubleClickRequestsNodeOnce),
                new TestCase("Designer node palette drag request carries descriptor", DesignerInteractionTests.NodePaletteDragRequestCarriesDescriptor),
                new TestCase("Designer stop marks running cards stopped", DesignerInteractionTests.StopMarksRunningCardsStopped),
                new TestCase("Designer debug buttons recover after stop", DesignerInteractionTests.DebugButtonsRecoverAfterStop),
                new TestCase("Designer embedded toolbar hides standalone document commands", DesignerInteractionTests.EmbeddedToolbarHidesStandaloneDocumentCommands),
                new TestCase("Designer modern theme and external toolbar are self-contained", DesignerInteractionTests.ModernThemeAndExternalToolbarAreSelfContained),
                new TestCase("Designer palette searches all descriptor fields and restores expansion", DesignerInteractionTests.PaletteSearchesAllDescriptorFieldsAndRestoresExpansion),
                new TestCase("Designer ports stay outside cards and edges end naturally", DesignerInteractionTests.PortsStayOutsideCardsAndEdgesEndNaturally),
                new TestCase("Designer canvas mini-map tracks viewport and clamps navigation", DesignerInteractionTests.CanvasMiniMapTracksViewportAndClampsNavigation),
                new TestCase("Designer canvas pan aligns offsets to device pixels", DesignerInteractionTests.CanvasPanAlignsOffsetsToDevicePixels),
                new TestCase("Designer canvas interaction coalesces high frequency updates", DesignerInteractionTests.CanvasInteractionCoalescesHighFrequencyUpdates),
                new TestCase("Designer debug drawer honors auto open and user preference", DesignerInteractionTests.DebugDrawerHonorsAutoOpenAndUserPreference),
                new TestCase("Designer property draft applies resets and resolves decisions", DesignerInteractionTests.PropertyDraftAppliesResetsAndResolvesDecisions),
                new TestCase("Designer property draft prompt releases the original node click", DesignerInteractionTests.PropertyDraftPromptDoesNotCaptureReleasedNodeClick),
                new TestCase("Designer property draft discard restores an invalid baseline once", DesignerInteractionTests.PropertyDraftDiscardRestoresInvalidBaselineOnce),
                new TestCase("Designer property draft rejects invalid text and survives refresh", DesignerInteractionTests.PropertyDraftRejectsInvalidTextAndSurvivesRefresh),
                new TestCase("Designer property draft apply button tracks validation state", DesignerInteractionTests.PropertyDraftApplyButtonTracksValidationState),
                new TestCase("Designer property panel keeps fields and footer separated", DesignerInteractionTests.PropertyPanelLayoutKeepsFieldsAndFooterSeparated),
                new TestCase("Designer required property errors fit above the footer", DesignerInteractionTests.PropertyPanelRequiredErrorFitsAboveFooterAtMinimumSize),
                new TestCase("Designer property draft guards load and debug mode", DesignerInteractionTests.PropertyDraftGuardsLoadAndDebugMode),
                new TestCase("Designer property draft preserves invalid dynamic candidates", DesignerInteractionTests.PropertyDraftPreservesInvalidDynamicCandidates),
                new TestCase("Designer dynamic descriptor draft refreshes and reconciles fields", DesignerInteractionTests.DynamicDescriptorDraftRefreshesAndReconcilesFields),
                new TestCase("Designer property draft validates variables and node switch decisions", DesignerInteractionTests.PropertyDraftValidatesVariablesAndNodeSwitchDecisions),
                new TestCase("Designer host API loads and captures deep copies", DesignerInteractionTests.HostDocumentApiLoadsCapturesAndDeepCopies),
                new TestCase("Designer host API resets to an empty document", DesignerInteractionTests.HostResetCreatesEmptyDocument),
                new TestCase("Designer host API publishes runtime file", DesignerInteractionTests.HostApiPublishesRuntimeFile),
                new TestCase("Designer palette default add uses viewport center", DesignerInteractionTests.PaletteDefaultAddUsesViewportCenter),
                new TestCase("Designer canvas zoom keeps viewport anchor stable", DesignerInteractionTests.CanvasZoomKeepsViewportAnchorStable),
                new TestCase("Designer node card uses sharp text rendering options", DesignerInteractionTests.NodeCardUsesSharpTextRenderingOptions),
                new TestCase("Designer palette and node card show descriptor descriptions", DesignerInteractionTests.PaletteAndNodeCardShowDescriptorDescription),
                new TestCase("Designer node card shows runtime summary inside card", DesignerInteractionTests.NodeCardShowsRuntimeSummaryAboveCard)
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
