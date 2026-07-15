using System;
using System.Collections.Generic;

namespace Vision.Flow.Core.Domain.Flows
{
    /// <summary>
    /// 节点执行策略。该模型属于稳定流程协议，会随设计态和运行态文件序列化。
    /// </summary>
    public sealed class NodeExecutionPolicy
    {
        public NodeExecutionPolicy()
        {
            TimeoutMs = 0;
            MaxConcurrentExecutions = 1;
            RetryPolicy = new RetryPolicy();
            FailureStrategy = FailureStrategy.StopFlow;
            DefaultOutputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 单次执行超时毫秒数；0 表示继承流程全局超时设置。
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// 此节点允许的最大并发执行数，默认为 1。本阶段仅固化协议，调度器将在后续阶段使用。
        /// </summary>
        public int MaxConcurrentExecutions { get; set; }

        /// <summary>
        /// 节点失败后的重试策略。
        /// </summary>
        public RetryPolicy RetryPolicy { get; set; }

        /// <summary>
        /// 重试耗尽后的失败处理策略。
        /// </summary>
        public FailureStrategy FailureStrategy { get; set; }

        /// <summary>
        /// 使用默认输出策略时写入变量池的输出值，键必须覆盖节点描述符声明的全部输出。
        /// </summary>
        public Dictionary<string, object> DefaultOutputs { get; set; }
    }

    /// <summary>
    /// 节点重试协议。MaxRetries 只统计首次执行后的重试次数。
    /// </summary>
    public sealed class RetryPolicy
    {
        public RetryPolicy()
        {
            Enabled = false;
            MaxRetries = 3;
            RetryIntervalMs = 1000;
        }

        /// <summary>
        /// 是否启用重试；false 时节点只执行一次。
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 首次执行失败后允许的最大重试次数，不包含首次执行。
        /// </summary>
        public int MaxRetries { get; set; }

        /// <summary>
        /// 两次尝试之间的固定等待毫秒数。
        /// </summary>
        public int RetryIntervalMs { get; set; }
    }

    /// <summary>
    /// 节点重试耗尽后的稳定失败处理协议。
    /// </summary>
    public enum FailureStrategy
    {
        StopFlow = 0,
        ErrorBranch = 1,
        DefaultOutputs = 2
    }
}
