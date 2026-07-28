using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Vision.Flow.Core.Contracts.Devices
{
    /// <summary>
    /// 协议无关的运控命令请求。
    /// </summary>
    public sealed class MotionAdapterCommandRequest
    {
        public MotionAdapterCommandRequest(
            string commandName,
            IReadOnlyDictionary<string, object> parameters = null,
            TimeSpan? responseTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("Motion command name is required.", "commandName");

            CommandName = commandName;
            Parameters = MotionAdapterDictionary.Copy(parameters);
            ResponseTimeout = responseTimeout;
        }

        public string CommandName { get; private set; }

        public IReadOnlyDictionary<string, object> Parameters { get; private set; }

        public TimeSpan? ResponseTimeout { get; private set; }
    }

    /// <summary>
    /// 协议无关的运控命令执行结果。
    /// </summary>
    public sealed class MotionAdapterCommandResult
    {
        public MotionAdapterCommandResult(
            string commandName,
            string sentPayload,
            string rawResponse,
            IReadOnlyDictionary<string, object> outputs = null)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("Motion command name is required.", "commandName");

            CommandName = commandName;
            SentPayload = sentPayload;
            RawResponse = rawResponse;
            Outputs = MotionAdapterDictionary.Copy(outputs);
        }

        public string CommandName { get; private set; }

        public string SentPayload { get; private set; }

        public string RawResponse { get; private set; }

        public IReadOnlyDictionary<string, object> Outputs { get; private set; }
    }

    /// <summary>
    /// 协议无关的运控接收命令事件。
    /// </summary>
    public sealed class MotionAdapterCommandReceivedEventArgs : EventArgs
    {
        public MotionAdapterCommandReceivedEventArgs(
            string motionId,
            string commandName,
            string wireCode,
            string rawText,
            IReadOnlyDictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(motionId))
                throw new ArgumentException("Motion id is required.", "motionId");
            if (string.IsNullOrWhiteSpace(commandName))
                throw new ArgumentException("Motion command name is required.", "commandName");

            MotionId = motionId;
            CommandName = commandName;
            WireCode = wireCode;
            RawText = rawText;
            Parameters = MotionAdapterDictionary.Copy(parameters);
        }

        public string MotionId { get; private set; }

        public string CommandName { get; private set; }

        public string WireCode { get; private set; }

        public string RawText { get; private set; }

        public IReadOnlyDictionary<string, object> Parameters { get; private set; }
    }

    internal static class MotionAdapterDictionary
    {
        public static IReadOnlyDictionary<string, object> Copy(
            IReadOnlyDictionary<string, object> source)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (source != null)
            {
                foreach (KeyValuePair<string, object> item in source)
                    values[item.Key] = item.Value;
            }

            return new ReadOnlyDictionary<string, object>(values);
        }
    }
}
