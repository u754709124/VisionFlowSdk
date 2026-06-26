using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vision.DeviceAdapters;
using Vision.Flow.Core;
using Vision.Flow.Nodes;

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
                new TestCase("Runtime serialization round-trips without view state", SerializationTests.RuntimeRoundTrip),
                new TestCase("Design serialization round-trips runtime and view state", SerializationTests.DesignRoundTrip),
                new TestCase("Legacy design serialization uses default canvas size", SerializationTests.DesignMissingCanvasSizeUsesDefaults),
                new TestCase("FlowValidator rejects duplicate NodeId", FlowValidationPublishTests.DuplicateNodeIdReturnsError),
                new TestCase("FlowValidator rejects dangling edges", FlowValidationPublishTests.DanglingEdgeReturnsError),
                new TestCase("FlowValidator rejects missing required settings", FlowValidationPublishTests.MissingRequiredSettingReturnsError),
                new TestCase("FlowValidator rejects missing binding outputs", FlowValidationPublishTests.MissingBindingOutputReturnsError),
                new TestCase("FlowValidator rejects invalid StreamFrames settings", FlowValidationPublishTests.InvalidStreamFramesSettingsReturnErrors),
                new TestCase("FlowValidator rejects invalid queue and group settings", FlowValidationPublishTests.InvalidQueueAndGroupSettingsReturnErrors),
                new TestCase("FlowPublishService removes designer view state", FlowValidationPublishTests.PublishRuntimeDoesNotContainViewState),
                new TestCase("FlowPublishService publishes a valid runtime", FlowValidationPublishTests.ValidFlowPublishesSuccessfully),
                new TestCase("Sample flow files deserialize and validate", SampleFlowTests.SampleFlowFilesDeserializeAndValidate),
                new TestCase("Continuous scan sample publishes runtime with enhanced rules", SampleFlowTests.ContinuousScanPublishesRuntimeWithEnhancedRules),
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
                new TestCase("FlowTaskQueue enforces capacity and publishes events", FlowTaskQueueTests.CapacityRejectsAndPublishesEvents),
                new TestCase("FlowTaskQueue supports drop stop and notify full modes", FlowTaskQueueTests.DropStopAndNotifyFullModes),
                new TestCase("FlowTaskQueueRegistry reuses named queues", FlowTaskQueueTests.RegistryReusesNamedQueues),
                new TestCase("DefaultDeviceRegistry resolves a fake camera", AdapterTests.RegistryGetsFakeCamera),
                new TestCase("FakeCameraAdapter soft trigger raises FrameArrived", AdapterTests.SoftTriggerReceivesFrame),
                new TestCase("FakeCameraAdapter cancellation prevents frame creation", AdapterTests.SoftTriggerCancellationPreventsFrame),
                new TestCase("FakeCameraAdapter can return before frame arrives", AdapterTests.SoftTriggerCanReturnBeforeFrameArrived),
                new TestCase("CameraFrameRouter duplicate register does not duplicate callbacks", AdapterTests.CameraFrameRouterDuplicateRegisterDoesNotDuplicateCallbacks),
                new TestCase("CameraFrameRouter unregister releases camera subscription", AdapterTests.CameraFrameRouterUnregisterReleasesSubscription),
                new TestCase("CameraFrameRouter dispose cancels waiters", AdapterTests.CameraFrameRouterDisposeCancelsWaiters),
                new TestCase("CameraFrameRouter stream subscription dispose stops callbacks", AdapterTests.CameraFrameRouterStreamSubscriptionDisposeStopsCallbacks),
                new TestCase("VisionImageReference supports clone and disposal", AdapterTests.VisionImageReferenceLifecycle),
                new TestCase("FakeVisionImage supports clone and disposal", AdapterTests.FakeVisionImageLifecycle),
                new TestCase("FakeRecipeAdapter returns OK", AdapterTests.FakeRecipeReturnsOk),
                new TestCase("FakeImageSaveAdapter returns a simulated path", AdapterTests.FakeImageSaveReturnsPath),
                new TestCase("FakeImageSaveAdapter snapshots image references", AdapterTests.FakeImageSaveSnapshotsImageReference),
                new TestCase("CommonNodeRegistration resolves common factories", CommonNodeTests.RegisterAllResolvesFactories),
                new TestCase("LogNode publishes a runtime log event", CommonNodeTests.LogNodePublishesRuntimeEvent),
                new TestCase("DelayNode executes a configured delay", CommonNodeTests.DelayNodeExecutes),
                new TestCase("VariableSetNode writes a variable subsequent node can read", CommonNodeTests.VariableSetNodeWritesVariableForNextNode),
                new TestCase("AndJoinNode triggers after two inputs with the same JoinKey", ControlFlowNodeTests.AndJoinTwoInputsSameJoinKey),
                new TestCase("AndJoinNode keeps different JoinKeys isolated", ControlFlowNodeTests.AndJoinDifferentKeysDoNotMix),
                new TestCase("AndJoinNode duplicate policy Error routes to Error", ControlFlowNodeTests.AndJoinDuplicatePolicyError),
                new TestCase("ConditionNode routes true and false branches", ControlFlowNodeTests.ConditionTrueFalseRoutes),
                new TestCase("MotionNotifyNode sends a fake motion message", MotionNodeTests.MotionNotifyWithFakeMotion),
                new TestCase("MotionMoveToNode moves fake motion", MotionNodeTests.MotionMoveToWithFakeMotion),
                new TestCase("MotionWaitInPositionNode waits fake motion", MotionNodeTests.MotionWaitInPositionWithFakeMotion),
                new TestCase("Motion node missing MotionId routes to Error", MotionNodeTests.MissingMotionIdRoutesError),
                new TestCase("Camera nodes set parameters, trigger, and receive a matching frame", CameraNodeTests.SetTriggerCallbackFlow),
                new TestCase("CameraImageCallbackNode times out on mismatched TriggerId", CameraNodeTests.ImageCallbackTimeoutWhenTriggerIdDoesNotMatch),
                new TestCase("CameraImageCallbackNode can match any next frame", CameraNodeTests.ImageCallbackAnyMatchMode),
                new TestCase("CameraImageCallbackNode stream mode collects frames", CameraNodeTests.ImageCallbackStreamFrames),
                new TestCase("CameraImageCallbackNode StreamFrames PerFrame dispatches each frame", CameraNodeTests.ImageCallbackStreamFramesPerFrame),
                new TestCase("Stage 07 nodes run callback recipe save and database chain", Stage07NodeTests.CallbackRecipeSaveDatabaseFlow),
                new TestCase("Stage 07 recipe save and database nodes can run through queues", Stage07NodeTests.QueuedRecipeSaveDatabaseFlow),
                new TestCase("Stage 07 recipe queue can return before completion", Stage07NodeTests.NonBlockingRecipeQueue),
                new TestCase("Stage 07 save and database queues can return before completion", Stage07NodeTests.NonBlockingSaveAndDatabaseQueues),
                new TestCase("Stage 08 FrameGroupJoin completes, sorts frames, and stitches", Stage08NodeTests.FrameGroupJoinSortsAndStitches),
                new TestCase("Stage 08 FrameGroupJoin detects duplicate ShotIndex", Stage08NodeTests.FrameGroupJoinDetectsDuplicateShotIndex),
                new TestCase("Stage 08 FrameGroupJoin supports bindings replace duplicates and continuous validation", Stage08NodeTests.FrameGroupJoinBindingsReplaceAndContinuousValidation),
                new TestCase("Stage 08 FrameGroupJoin detects non-continuous ShotIndex", Stage08NodeTests.FrameGroupJoinDetectsNonContinuousShotIndex),
                new TestCase("Stage 08 ScanGroupJoin sorts preprocess results and fusion outputs images", Stage08NodeTests.ScanGroupJoinSortsAndFusionOutputsImages),
                new TestCase("Stage 08 ScanGroupJoin supports bindings replace duplicates and fusion binding", Stage08NodeTests.ScanGroupJoinBindingsReplaceAndFusionBinding),
                new TestCase("Stage 08 ScanGroupJoin detects non-continuous FrameIndex", Stage08NodeTests.ScanGroupJoinDetectsNonContinuousFrameIndex),
                new TestCase("Designer property panel read-only mode disables editors", DesignerInteractionTests.PropertyPanelReadOnlyDisablesEditors),
                new TestCase("Designer node palette read-only mode blocks node requests", DesignerInteractionTests.NodePaletteReadOnlyBlocksNodeRequests),
                new TestCase("Designer node palette single click selects only", DesignerInteractionTests.NodePaletteSingleClickSelectsOnly),
                new TestCase("Designer node palette double click requests node once", DesignerInteractionTests.NodePaletteDoubleClickRequestsNodeOnce),
                new TestCase("Designer node palette drag request carries descriptor", DesignerInteractionTests.NodePaletteDragRequestCarriesDescriptor),
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
