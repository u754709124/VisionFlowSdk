using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.Flow.Nodes
{
    // 图像回调节点等待路由后的帧，并为下游节点发布帧和图像变量。
    public sealed class CameraImageCallbackNodeConfig
    {
        public CameraImageCallbackNodeConfig()
        {
            TimeoutMs = 1000;
            FrameTimeoutMs = 1000;
            ExpectedFrameCount = 1;
            CallbackMode = "WaitNextFrame";
            MatchMode = "TriggerId";
            StreamOutputMode = "Batch";
            AutoStopAfterExpectedFrameCount = true;
            FrameIndexSource = "Increment";
        }

        public string CameraId { get; set; }

        public string TriggerId { get; set; }

        public string CallbackMode { get; set; }

        public string MatchMode { get; set; }

        public string StreamOutputMode { get; set; }

        public string ScanGroupIdBinding { get; set; }

        public int TimeoutMs { get; set; }

        public int ExpectedFrameCount { get; set; }

        public int FrameTimeoutMs { get; set; }

        public bool AutoStopAfterExpectedFrameCount { get; set; }

        public string FrameIndexSource { get; set; }

        public int StartFrameIndex { get; set; }
    }

    public sealed class CameraImageCallbackNodeFactory : BaseNodeFactory<CameraImageCallbackNodeConfig>
    {
        public const string TypeName = FlowNodeTypes.CameraImageCallback;

        public override string NodeType
        {
            get { return TypeName; }
        }

        public override NodeDescriptor Descriptor
        {
            get { return CameraImageCallbackNodeDescriptor.Create(); }
        }

        protected override CameraImageCallbackNodeConfig CreateConfig(NodeDefinition definition)
        {
            return new CameraImageCallbackNodeConfig
            {
                CameraId = GetStringSetting(definition, "CameraId", null),
                TriggerId = GetStringSetting(definition, "TriggerId", null),
                CallbackMode = GetStringSetting(definition, "CallbackMode", "WaitNextFrame"),
                MatchMode = GetStringSetting(definition, "MatchMode", "TriggerId"),
                StreamOutputMode = GetStringSetting(definition, "StreamOutputMode", "Batch"),
                ScanGroupIdBinding = GetStringSetting(definition, "ScanGroupIdBinding", null),
                TimeoutMs = GetInt32Setting(definition, "TimeoutMs", 1000),
                ExpectedFrameCount = GetInt32Setting(definition, "ExpectedFrameCount", 1),
                FrameTimeoutMs = GetInt32Setting(definition, "FrameTimeoutMs", 1000),
                AutoStopAfterExpectedFrameCount = Convert.ToBoolean(GetSetting(definition, "AutoStopAfterExpectedFrameCount", true), CultureInfo.InvariantCulture),
                FrameIndexSource = GetStringSetting(definition, "FrameIndexSource", "Increment"),
                StartFrameIndex = GetInt32Setting(definition, "StartFrameIndex", 0)
            };
        }

        protected override IFlowNode CreateNode(NodeDefinition definition, CameraImageCallbackNodeConfig config)
        {
            return new CameraImageCallbackNode(config);
        }
    }

    public sealed class CameraImageCallbackNode : IFlowNode
    {
        private readonly CameraImageCallbackNodeConfig _config;

        public CameraImageCallbackNode(CameraImageCallbackNodeConfig config)
        {
            _config = config ?? new CameraImageCallbackNodeConfig();
        }

        public async Task<NodeExecutionResult> ExecuteAsync(FlowExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cameraId = CameraNodeHelpers.ResolveString(context, "CameraId", _config.CameraId);
            if (string.IsNullOrWhiteSpace(cameraId))
            {
                return NodeExecutionResult.Failure("CameraId is required.");
            }

            var timeoutMs = CameraNodeHelpers.ResolveInt32(context, "TimeoutMs", _config.TimeoutMs);
            if (timeoutMs < 0)
            {
                return NodeExecutionResult.Failure("TimeoutMs must be greater than or equal to zero.");
            }

            var callbackMode = CameraNodeHelpers.ResolveString(context, "CallbackMode", _config.CallbackMode);
            if (string.IsNullOrWhiteSpace(callbackMode))
            {
                callbackMode = "WaitNextFrame";
            }

            var matchMode = CameraNodeHelpers.ResolveString(context, "MatchMode", _config.MatchMode);
            if (string.IsNullOrWhiteSpace(matchMode))
            {
                matchMode = "TriggerId";
            }

            var camera = context.Devices.GetCamera(cameraId);
            context.CameraFrames.EnsureCamera(camera, cameraId);

            var ticket = CreateWaitTicket(context, cameraId, matchMode);
            if (ticket == null)
            {
                return NodeExecutionResult.Failure("CameraImageCallbackNode supports TriggerId, Any, and ScanGroupId match modes.");
            }

            if (string.Equals(callbackMode, "StreamFrames", StringComparison.OrdinalIgnoreCase))
            {
                var streamOutputMode = CameraNodeHelpers.ResolveString(context, "StreamOutputMode", _config.StreamOutputMode);
                if (string.Equals(streamOutputMode, "PerFrame", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExecuteStreamFramesPerFrameAsync(context, camera, ticket, timeoutMs, cancellationToken).ConfigureAwait(false);
                }

                return await ExecuteStreamFramesBatchAsync(context, camera, ticket, timeoutMs, cancellationToken).ConfigureAwait(false);
            }

            if (!string.Equals(callbackMode, "WaitNextFrame", StringComparison.OrdinalIgnoreCase))
            {
                return NodeExecutionResult.Failure("Unsupported camera callback mode: " + callbackMode);
            }

            CameraFrameData frame;
            try
            {
                frame = await context.CameraFrames.WaitForFrameAsync(
                    camera,
                    ticket,
                    timeoutMs,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (frame == null)
            {
                return NodeExecutionResult.Timeout(
                    "Timed out waiting for camera frame. CameraId=" + cameraId + ", " + ticket.Describe(),
                    "Timeout");
            }

            return CreateFrameResult(context, frame, null);
        }

        private async Task<NodeExecutionResult> ExecuteStreamFramesBatchAsync(
            FlowExecutionContext context,
            ICameraAdapter camera,
            CameraFrameWaitTicket ticket,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var expectedFrameCount = CameraNodeHelpers.ResolveInt32(context, "ExpectedFrameCount", _config.ExpectedFrameCount);
            if (expectedFrameCount <= 0)
            {
                return NodeExecutionResult.Failure("ExpectedFrameCount must be greater than zero.");
            }

            var frameTimeoutMs = CameraNodeHelpers.ResolveInt32(context, "FrameTimeoutMs", _config.FrameTimeoutMs);
            if (frameTimeoutMs < 0)
            {
                return NodeExecutionResult.Failure("FrameTimeoutMs must be greater than or equal to zero.");
            }

            if (frameTimeoutMs == 0)
            {
                frameTimeoutMs = timeoutMs;
            }

            var frames = new List<CameraFrameData>();
            using (var subscription = context.CameraFrames.Subscribe(camera, ticket))
            {
                for (var index = 0; index < expectedFrameCount; index++)
                {
                    var frame = await subscription.WaitForNextFrameAsync(frameTimeoutMs, cancellationToken).ConfigureAwait(false);
                    if (frame == null)
                    {
                        return NodeExecutionResult.Timeout(
                            "Timed out waiting for camera stream frame. CameraId=" + ticket.CameraId + ", " + ticket.Describe(),
                            "Timeout");
                    }

                    frames.Add(frame);
                }
            }

            return CreateFrameResult(context, frames[frames.Count - 1], frames);
        }

        private async Task<NodeExecutionResult> ExecuteStreamFramesPerFrameAsync(
            FlowExecutionContext context,
            ICameraAdapter camera,
            CameraFrameWaitTicket ticket,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var expectedFrameCount = CameraNodeHelpers.ResolveInt32(context, "ExpectedFrameCount", _config.ExpectedFrameCount);
            if (expectedFrameCount < 0)
            {
                return NodeExecutionResult.Failure("ExpectedFrameCount must be greater than or equal to zero.");
            }

            var frameTimeoutMs = CameraNodeHelpers.ResolveInt32(context, "FrameTimeoutMs", _config.FrameTimeoutMs);
            if (frameTimeoutMs < 0)
            {
                return NodeExecutionResult.Failure("FrameTimeoutMs must be greater than or equal to zero.");
            }

            if (frameTimeoutMs == 0)
            {
                frameTimeoutMs = timeoutMs;
            }

            var autoStop = CameraNodeHelpers.ResolveBoolean(context, "AutoStopAfterExpectedFrameCount", _config.AutoStopAfterExpectedFrameCount);
            var startFrameIndex = CameraNodeHelpers.ResolveInt32(context, "StartFrameIndex", _config.StartFrameIndex);
            var frameIndexSource = CameraNodeHelpers.ResolveString(context, "FrameIndexSource", _config.FrameIndexSource);
            var scanGroupId = ResolveScanGroupId(context);
            var frames = new List<CameraFrameData>();
            var dispatchedCount = 0;

            using (var subscription = context.CameraFrames.Subscribe(camera, ticket))
            {
                subscription.MaxFrameCount = autoStop ? expectedFrameCount : 0;
                subscription.ScanGroupId = scanGroupId;

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (autoStop && expectedFrameCount > 0 && dispatchedCount >= expectedFrameCount)
                    {
                        break;
                    }

                    var frame = await subscription.WaitForNextFrameAsync(frameTimeoutMs, cancellationToken).ConfigureAwait(false);
                    if (frame == null)
                    {
                        return NodeExecutionResult.Timeout(
                            "Timed out waiting for camera stream frame. CameraId=" + ticket.CameraId + ", " + ticket.Describe(),
                            "Timeout");
                    }

                    var frameIndex = ResolveFrameIndex(frame, frameIndexSource, startFrameIndex + dispatchedCount);
                    ApplyFrameMetadata(frame, scanGroupId, frameIndex);
                    var frameToken = CreateFrameToken(context.Token, frame, scanGroupId, frameIndex, dispatchedCount);
                    var outputs = CreateFrameOutputs(frame, null);
                    outputs["FrameIndex"] = frameIndex;
                    outputs["ScanGroupId"] = scanGroupId;
                    frames.Add(frame);
                    dispatchedCount++;

                    await context.Continuations.DispatchAsync(
                        new FlowContinuation
                        {
                            SourceNodeId = context.Node.Id,
                            OutputPort = "Frame",
                            Token = frameToken,
                            Variables = context.Variables,
                            Outputs = outputs,
                            FlowRunId = context.FlowRunId
                        },
                        cancellationToken).ConfigureAwait(false);

                    if (!autoStop && expectedFrameCount == 0)
                    {
                        continue;
                    }
                }
            }

            var completedOutputs = new Dictionary<string, object>
            {
                { "FrameCount", dispatchedCount },
                { "Frames", frames },
                { "ScanGroupId", scanGroupId }
            };
            return NodeExecutionResult.Success("Completed", completedOutputs);
        }

        private CameraFrameWaitTicket CreateWaitTicket(FlowExecutionContext context, string cameraId, string matchMode)
        {
            if (string.Equals(matchMode, CameraFrameMatchModes.TriggerId, StringComparison.OrdinalIgnoreCase))
            {
                var triggerId = ResolveTriggerId(context);
                if (string.IsNullOrWhiteSpace(triggerId))
                {
                    throw new InvalidOperationException("TriggerId is required for camera image callback matching.");
                }

                return new CameraFrameWaitTicket
                {
                    CameraId = cameraId,
                    MatchMode = CameraFrameMatchModes.TriggerId,
                    TriggerId = triggerId
                };
            }

            if (string.Equals(matchMode, CameraFrameMatchModes.Any, StringComparison.OrdinalIgnoreCase))
            {
                return new CameraFrameWaitTicket
                {
                    CameraId = cameraId,
                    MatchMode = CameraFrameMatchModes.Any
                };
            }

            if (string.Equals(matchMode, CameraFrameMatchModes.ScanGroupId, StringComparison.OrdinalIgnoreCase))
            {
                var scanGroupId = ResolveScanGroupId(context);
                if (string.IsNullOrWhiteSpace(scanGroupId))
                {
                    throw new InvalidOperationException("ScanGroupIdBinding must resolve to a value when MatchMode is ScanGroupId.");
                }

                return new CameraFrameWaitTicket
                {
                    CameraId = cameraId,
                    MatchMode = CameraFrameMatchModes.ScanGroupId,
                    ScanGroupId = scanGroupId
                };
            }

            if (string.Equals(matchMode, CameraFrameMatchModes.TimeWindow, StringComparison.OrdinalIgnoreCase))
            {
                return new CameraFrameWaitTicket
                {
                    CameraId = cameraId,
                    MatchMode = CameraFrameMatchModes.TimeWindow,
                    NotBeforeUtc = DateTime.UtcNow
                };
            }

            return null;
        }

        private NodeExecutionResult CreateFrameResult(
            FlowExecutionContext context,
            CameraFrameData frame,
            IList<CameraFrameData> frames)
        {
            if (!string.IsNullOrWhiteSpace(frame.FrameId))
            {
                context.Token.FrameId = frame.FrameId;
                context.Token.Set("FrameId", frame.FrameId);
            }

            var outputs = CreateFrameOutputs(frame, frames);

            if (frames != null)
            {
                outputs["Frames"] = frames;
                outputs["FrameCount"] = frames.Count;
            }

            return NodeExecutionResult.Success("Next", outputs);
        }

        private static Dictionary<string, object> CreateFrameOutputs(CameraFrameData frame, IList<CameraFrameData> frames)
        {
            var metadata = frame.Metadata ?? new Dictionary<string, object>();
            return new Dictionary<string, object>
            {
                { "Image", frame.Image },
                { "Frame", frame },
                { "FrameId", frame.FrameId },
                { "GrabTime", frame.GrabTime },
                { "Metadata", metadata },
                { "CameraId", frame.CameraId },
                { "TriggerId", frame.TriggerId }
            };
        }

        private static void ApplyFrameMetadata(CameraFrameData frame, string scanGroupId, int frameIndex)
        {
            if (frame == null)
            {
                return;
            }

            if (frame.Metadata == null)
            {
                frame.Metadata = new Dictionary<string, object>();
            }

            frame.Metadata[FlowMetadataKeys.FrameIndex] = frameIndex;
            if (!string.IsNullOrWhiteSpace(scanGroupId))
            {
                frame.Metadata[FlowMetadataKeys.ScanGroupId] = scanGroupId;
            }

            if (frame.Image != null && frame.Image.Metadata != null)
            {
                frame.Image.Metadata[FlowMetadataKeys.FrameIndex] = frameIndex;
                if (!string.IsNullOrWhiteSpace(scanGroupId))
                {
                    frame.Image.Metadata[FlowMetadataKeys.ScanGroupId] = scanGroupId;
                }
            }
        }

        private static int ResolveFrameIndex(CameraFrameData frame, string frameIndexSource, int fallback)
        {
            if (frame != null &&
                frame.Metadata != null &&
                !string.Equals(frameIndexSource, "Increment", StringComparison.OrdinalIgnoreCase))
            {
                object value;
                if (frame.Metadata.TryGetValue("FrameIndex", out value) ||
                    frame.Metadata.TryGetValue("TriggerIndex", out value) ||
                    frame.Metadata.TryGetValue("Encoder", out value))
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
            }

            return fallback;
        }

        private static FlowToken CreateFrameToken(
            FlowToken source,
            CameraFrameData frame,
            string scanGroupId,
            int frameIndex,
            int dispatchedIndex)
        {
            var token = new FlowToken
            {
                TokenId = source == null || string.IsNullOrWhiteSpace(source.TokenId)
                    ? Guid.NewGuid().ToString("N")
                    : source.TokenId + "-frame-" + frameIndex.ToString(CultureInfo.InvariantCulture),
                ProductId = source == null ? null : source.ProductId,
                WorkpieceId = source == null ? null : source.WorkpieceId,
                PositionId = source == null ? null : source.PositionId,
                CaptureGroupId = source == null ? null : source.CaptureGroupId,
                ScanGroupId = string.IsNullOrWhiteSpace(scanGroupId) ? (source == null ? null : source.ScanGroupId) : scanGroupId,
                FrameId = frame == null ? null : frame.FrameId
            };

            if (source != null && source.Metadata != null)
            {
                foreach (var item in source.Metadata)
                {
                    token.Metadata[item.Key] = item.Value;
                }
            }

            if (source != null && source.Values != null)
            {
                foreach (var item in source.Values)
                {
                    token.Values[item.Key] = item.Value;
                }
            }

            token.Set("CameraId", frame == null ? null : frame.CameraId);
            token.Set("Frame", frame);
            token.Set("Image", frame == null ? null : frame.Image);
            token.Set("FrameId", frame == null ? null : frame.FrameId);
            token.Set("FrameIndex", frameIndex);
            token.Set("FrameSequence", dispatchedIndex);
            token.Set("TriggerId", frame == null ? null : frame.TriggerId);
            token.Set("ScanGroupId", token.ScanGroupId);
            return token;
        }

        private string ResolveScanGroupId(FlowExecutionContext context)
        {
            var binding = CameraNodeHelpers.ResolveString(context, "ScanGroupIdBinding", _config.ScanGroupIdBinding);
            if (!string.IsNullOrWhiteSpace(binding))
            {
                var value = ControlFlowNodeHelpers.ResolveBindingExpression(context, binding);
                return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            var inputValue = context.GetInputValue("ScanGroupId");
            if (inputValue != null)
            {
                return Convert.ToString(inputValue, CultureInfo.InvariantCulture);
            }

            return string.IsNullOrWhiteSpace(context.Token.ScanGroupId) ? null : context.Token.ScanGroupId;
        }

        private string ResolveTriggerId(FlowExecutionContext context)
        {
            var value = context.GetInputValue("TriggerId");
            var triggerId = value == null ? _config.TriggerId : Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(triggerId))
            {
                return triggerId;
            }

            string tokenTriggerId;
            if (context.Token.TryGet("TriggerId", out tokenTriggerId))
            {
                return tokenTriggerId;
            }

            return null;
        }
    }

    public static class CameraImageCallbackNodeDescriptor
    {
        public static NodeDescriptor Create()
        {
            return new NodeDescriptor
            {
                NodeType = CameraImageCallbackNodeFactory.TypeName,
                DisplayName = "Camera Image Callback",
                Category = "Camera",
                Version = "1.0.0",
                Description = "Waits for a frame matching the trigger id from a camera adapter.",
                InputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "In",
                        DisplayName = "In",
                        Direction = "Input",
                        DataType = "Control",
                        IsRequired = true,
                        Description = "Execution input."
                    }
                },
                OutputPorts =
                {
                    new NodePortDescriptor
                    {
                        Name = "Next",
                        DisplayName = "Next",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Continues when a matching frame is received."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Frame",
                        DisplayName = "Frame",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes each frame when StreamFrames uses PerFrame output."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Completed",
                        DisplayName = "Completed",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes when StreamFrames PerFrame finishes the expected frame count."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Timeout",
                        DisplayName = "Timeout",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes frame wait timeout."
                    },
                    new NodePortDescriptor
                    {
                        Name = "Error",
                        DisplayName = "Error",
                        Direction = "Output",
                        DataType = "Control",
                        Description = "Routes invalid configuration or adapter errors."
                    }
                },
                Settings =
                {
                    new NodeSettingDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = true,
                        Description = "Registered camera adapter id."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "CallbackMode",
                        DisplayName = "Callback Mode",
                        DataType = "String",
                        DefaultValue = "WaitNextFrame",
                        IsRequired = false,
                        Description = "WaitNextFrame waits for one frame. StreamFrames subscribes and collects one or more frames."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "StreamOutputMode",
                        DisplayName = "Stream Output",
                        DataType = "String",
                        DefaultValue = "Batch",
                        IsRequired = false,
                        Description = "Batch outputs Frames once. PerFrame dispatches each frame through the Frame output port."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TriggerId",
                        DisplayName = "Trigger Id",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Trigger id to match. This can also come from a binding or token value."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "MatchMode",
                        DisplayName = "Match Mode",
                        DataType = "String",
                        DefaultValue = "TriggerId",
                        IsRequired = true,
                        Description = "Frame matching mode: TriggerId, Any, or ScanGroupId."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "ScanGroupIdBinding",
                        DisplayName = "Scan Group Binding",
                        DataType = "String",
                        DefaultValue = null,
                        IsRequired = false,
                        Description = "Expression used when MatchMode is ScanGroupId, for example {{ token.ScanGroupId }}."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "TimeoutMs",
                        DisplayName = "Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 1000,
                        IsRequired = true,
                        Description = "Maximum time to wait for the frame. Zero disables the node timeout."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "ExpectedFrameCount",
                        DisplayName = "Expected Frames",
                        DataType = "Int32",
                        DefaultValue = 1,
                        IsRequired = false,
                        Description = "Number of frames to collect when CallbackMode is StreamFrames."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "FrameTimeoutMs",
                        DisplayName = "Frame Timeout (ms)",
                        DataType = "Int32",
                        DefaultValue = 1000,
                        IsRequired = false,
                        Description = "Per-frame timeout when CallbackMode is StreamFrames. Zero uses TimeoutMs."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "AutoStopAfterExpectedFrameCount",
                        DisplayName = "Auto Stop",
                        DataType = "Boolean",
                        DefaultValue = true,
                        IsRequired = false,
                        Description = "When true, StreamFrames PerFrame completes after ExpectedFrameCount frames."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "FrameIndexSource",
                        DisplayName = "Frame Index Source",
                        DataType = "String",
                        DefaultValue = "Increment",
                        IsRequired = false,
                        Description = "Increment or Metadata."
                    },
                    new NodeSettingDescriptor
                    {
                        Name = "StartFrameIndex",
                        DisplayName = "Start Frame Index",
                        DataType = "Int32",
                        DefaultValue = 0,
                        IsRequired = false,
                        Description = "First frame index when FrameIndexSource is Increment."
                    }
                },
                Outputs =
                {
                    new NodeOutputDescriptor
                    {
                        Name = "Image",
                        DisplayName = "Image",
                        DataType = "IVisionImage",
                        Description = "Captured image."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Frame",
                        DisplayName = "Frame",
                        DataType = "CameraFrameData",
                        Description = "Complete frame data."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "FrameId",
                        DisplayName = "Frame Id",
                        DataType = "String",
                        Description = "Captured frame id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "GrabTime",
                        DisplayName = "Grab Time",
                        DataType = "DateTime",
                        Description = "UTC image grab time."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Metadata",
                        DisplayName = "Metadata",
                        DataType = "Object",
                        Description = "Frame metadata."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "CameraId",
                        DisplayName = "Camera",
                        DataType = "String",
                        Description = "Frame camera id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "TriggerId",
                        DisplayName = "Trigger Id",
                        DataType = "String",
                        Description = "Frame trigger id."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "Frames",
                        DisplayName = "Frames",
                        DataType = "Object",
                        Description = "Collected frames when CallbackMode is StreamFrames."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "FrameCount",
                        DisplayName = "Frame Count",
                        DataType = "Int32",
                        Description = "Number of collected frames when CallbackMode is StreamFrames."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "FrameIndex",
                        DisplayName = "Frame Index",
                        DataType = "Int32",
                        Description = "Per-frame index when StreamOutputMode is PerFrame."
                    },
                    new NodeOutputDescriptor
                    {
                        Name = "ScanGroupId",
                        DisplayName = "Scan Group",
                        DataType = "String",
                        Description = "Resolved scan group id for stream frames."
                    }
                }
            };
        }
    }
}
