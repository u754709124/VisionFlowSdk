using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Vision.Flow.Core;

namespace Vision.DeviceAdapters
{
    // Fake motion adapters simulate move, wait, and motion message behavior for flow tests.
    public sealed class FakeMotionAdapter : IMotionAdapter
    {
        private readonly object _gate = new object();

        public FakeMotionAdapter(string motionId)
        {
            if (string.IsNullOrWhiteSpace(motionId))
            {
                throw new ArgumentException("Motion id is required.", "motionId");
            }

            MotionId = motionId;
            MoveDelayMs = 0;
        }

        public string MotionId { get; private set; }

        public string CurrentPosition { get; private set; }

        public int MoveDelayMs { get; set; }

        public MotionMessage LastMessage { get; private set; }

        public event EventHandler<MotionEventArgs> MotionEventReceived;

        public async Task MoveToAsync(string positionName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new ArgumentException("Position name is required.", "positionName");
            }

            if (MoveDelayMs > 0)
            {
                await Task.Delay(MoveDelayMs, cancellationToken).ConfigureAwait(false);
            }

            lock (_gate)
            {
                CurrentPosition = positionName;
            }

            RaiseMotionEvent(new MotionEventArgs
            {
                MotionId = MotionId,
                EventType = "MoveCompleted",
                PositionId = positionName
            });
        }

        public async Task WaitForInPositionAsync(string positionName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new ArgumentException("Position name is required.", "positionName");
            }

            string currentPosition;
            lock (_gate)
            {
                currentPosition = CurrentPosition;
            }

            if (!string.Equals(currentPosition, positionName, StringComparison.OrdinalIgnoreCase))
            {
                await MoveToAsync(positionName, cancellationToken).ConfigureAwait(false);
            }

            RaiseMotionEvent(new MotionEventArgs
            {
                MotionId = MotionId,
                EventType = "InPosition",
                PositionId = positionName
            });
        }

        public Task SendMessageAsync(MotionMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            var copy = CloneMessage(message);
            if (string.IsNullOrWhiteSpace(copy.MotionId))
            {
                copy.MotionId = MotionId;
            }

            lock (_gate)
            {
                LastMessage = copy;
            }

            return Task.FromResult(0);
        }

        public MotionMessage SnapshotLastMessage()
        {
            lock (_gate)
            {
                return LastMessage == null ? null : CloneMessage(LastMessage);
            }
        }

        public void RaiseMotionEvent(MotionEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }

            if (string.IsNullOrWhiteSpace(args.MotionId))
            {
                args.MotionId = MotionId;
            }

            var handler = MotionEventReceived;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        private static MotionMessage CloneMessage(MotionMessage message)
        {
            var clone = new MotionMessage
            {
                MessageType = message.MessageType,
                MotionId = message.MotionId,
                PositionId = message.PositionId,
                CaptureGroupId = message.CaptureGroupId,
                ScanGroupId = message.ScanGroupId,
                TokenId = message.TokenId,
                Result = message.Result
            };

            if (message.Metadata != null)
            {
                foreach (var item in message.Metadata)
                {
                    clone.Metadata[item.Key] = item.Value;
                }
            }

            return clone;
        }
    }
}
