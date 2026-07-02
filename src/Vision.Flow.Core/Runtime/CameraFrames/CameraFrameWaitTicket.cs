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

        public DateTime? NotBeforeUtc { get; set; }

        public CameraFrameWaitTicket Clone()
        {
            return new CameraFrameWaitTicket
            {
                CameraId = CameraId,
                MatchMode = MatchMode,
                TriggerId = TriggerId,
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
                default:
                    return "MatchMode=" + FlowEnumConverter.ToWireValue(MatchMode);
            }
        }
    }
}
