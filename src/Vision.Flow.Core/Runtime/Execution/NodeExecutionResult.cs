using System;
using System.Collections.Generic;
using System.Threading;
using Vision.Flow.Core.Domain.Nodes;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 节点执行结果，包含路由端口、输出变量和错误/超时状态。
    /// </summary>
    public sealed class NodeExecutionResult
    {
        public NodeExecutionResult()
        {
            OutputPort = FlowPortNames.Next;
            Outputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            FailureKind = NodeFailureKind.None;
        }

        public bool IsSuccess { get; set; }

        public bool IsTimeout { get; set; }

        public string OutputPort { get; set; }

        public string ErrorMessage { get; set; }

        /// <summary>
        /// 本次执行的失败分类；成功结果为 None。
        /// </summary>
        public NodeFailureKind FailureKind { get; set; }

        public Dictionary<string, object> Outputs { get; set; }

        public static NodeExecutionResult Success(string outputPort = FlowPortNames.Next, IDictionary<string, object> outputs = null)
        {
            return new NodeExecutionResult
            {
                IsSuccess = true,
                FailureKind = NodeFailureKind.None,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Next : outputPort,
                Outputs = outputs == null
                    ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, object>(outputs, StringComparer.OrdinalIgnoreCase)
            };
        }

        public static NodeExecutionResult Failure(
            string errorMessage,
            string outputPort = FlowPortNames.Error,
            NodeFailureKind failureKind = NodeFailureKind.Execution)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                FailureKind = failureKind == NodeFailureKind.None ? NodeFailureKind.Execution : failureKind,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Error : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };
        }

        public static NodeExecutionResult Timeout(string errorMessage = null, string outputPort = FlowPortNames.Error)
        {
            return new NodeExecutionResult
            {
                IsSuccess = false,
                IsTimeout = true,
                FailureKind = NodeFailureKind.Timeout,
                OutputPort = string.IsNullOrWhiteSpace(outputPort) ? FlowPortNames.Error : outputPort,
                ErrorMessage = errorMessage,
                Outputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
