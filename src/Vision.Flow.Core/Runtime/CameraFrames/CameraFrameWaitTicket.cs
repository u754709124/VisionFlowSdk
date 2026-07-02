using System;
using Vision.Flow.Core.Contracts.Devices;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.CameraFrames
{
    public sealed class CameraFrameWaitTicket
    {
        public CameraFrameWaitTicket()
        {
            MatchMode = CameraFrameMatchMode.TriggerId;
        }

        public string CameraId { get; set; }

        public CameraFrameMatchMode MatchMode { get; set; }

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

            switch (MatchMode)
            {
                case CameraFrameMatchMode.Any:
                    return true;
                case CameraFrameMatchMode.TriggerId:
                    return !string.IsNullOrWhiteSpace(TriggerId) &&
                        string.Equals(frame.TriggerId, TriggerId, StringComparison.OrdinalIgnoreCase);
                case CameraFrameMatchMode.ScanGroupId:
                    var frameScanGroupId = GetMetadataString(frame, FlowMetadataKeys.ScanGroupId);
                    return !string.IsNullOrWhiteSpace(ScanGroupId) &&
                        string.Equals(frameScanGroupId, ScanGroupId, StringComparison.OrdinalIgnoreCase);
                case CameraFrameMatchMode.TimeWindow:
                    return NotBeforeUtc.HasValue;
                default:
                    return false;
            }
        }

        public string Describe()
        {
            switch (MatchMode)
            {
                case CameraFrameMatchMode.TriggerId:
                    return "TriggerId=" + TriggerId;
                case CameraFrameMatchMode.ScanGroupId:
                    return "ScanGroupId=" + ScanGroupId;
                default:
                    return "MatchMode=" + FlowEnumConverter.ToWireValue(MatchMode);
            }
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
