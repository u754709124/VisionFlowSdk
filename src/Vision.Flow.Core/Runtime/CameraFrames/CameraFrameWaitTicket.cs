using System;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
    public sealed class CameraFrameWaitTicket
    {
        public CameraFrameWaitTicket()
        {
            MatchMode = CameraFrameMatchModes.TriggerId;
        }

        public string CameraId { get; set; }

        public string MatchMode { get; set; }

        public string TriggerId { get; set; }

        public string ScanGroupId { get; set; }

        public DateTime? NotBeforeUtc { get; set; }

        public CameraFrameWaitTicket Clone()
        {
            return new CameraFrameWaitTicket
            {
                CameraId = CameraId,
                MatchMode = MatchMode,
                TriggerId = TriggerId,
                ScanGroupId = ScanGroupId,
                NotBeforeUtc = NotBeforeUtc
            };
        }

        public bool Matches(CameraFrameData frame)
        {
            if (frame == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(CameraId) &&
                !string.Equals(frame.CameraId, CameraId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (NotBeforeUtc.HasValue &&
                frame.GrabTime != default(DateTime) &&
                frame.GrabTime < NotBeforeUtc.Value)
            {
                return false;
            }

            var mode = string.IsNullOrWhiteSpace(MatchMode) ? CameraFrameMatchModes.TriggerId : MatchMode;
            if (string.Equals(mode, CameraFrameMatchModes.Any, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(mode, CameraFrameMatchModes.TriggerId, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(TriggerId) &&
                    string.Equals(frame.TriggerId, TriggerId, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(mode, CameraFrameMatchModes.ScanGroupId, StringComparison.OrdinalIgnoreCase))
            {
                var frameScanGroupId = GetMetadataString(frame, FlowMetadataKeys.ScanGroupId);
                return !string.IsNullOrWhiteSpace(ScanGroupId) &&
                    string.Equals(frameScanGroupId, ScanGroupId, StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(mode, CameraFrameMatchModes.TimeWindow, StringComparison.OrdinalIgnoreCase))
            {
                return NotBeforeUtc.HasValue;
            }

            return false;
        }

        public string Describe()
        {
            var mode = string.IsNullOrWhiteSpace(MatchMode) ? CameraFrameMatchModes.TriggerId : MatchMode;
            if (string.Equals(mode, CameraFrameMatchModes.TriggerId, StringComparison.OrdinalIgnoreCase))
            {
                return "TriggerId=" + TriggerId;
            }

            if (string.Equals(mode, CameraFrameMatchModes.ScanGroupId, StringComparison.OrdinalIgnoreCase))
            {
                return "ScanGroupId=" + ScanGroupId;
            }

            return "MatchMode=" + mode;
        }

        private static string GetMetadataString(CameraFrameData frame, string name)
        {
            if (frame == null || frame.Metadata == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            object value;
            return frame.Metadata.TryGetValue(name, out value) ? Convert.ToString(value) : null;
        }
    }
}
