using System;
using System.Collections.Generic;
using Vision.Flow.Core.Runtime.State;

namespace Vision.Flow.Core.Runtime.Execution
{
    /// <summary>
    /// 一次运行请求的实际触发来源，必须与入口声明的触发类型一致。
    /// </summary>
    public enum FlowTriggerSource
    {
        Manual = 0,
        External = 1,
        NodeEvent = 2
    }

    /// <summary>
    /// 统一流程触发请求，手动调用和外部宿主均通过该模型进入运行时。
    /// </summary>
    public sealed class FlowTriggerRequest
    {
        public FlowTriggerRequest()
        {
            Source = FlowTriggerSource.Manual;
            Inputs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 需要触发的入口协议名。
        /// </summary>
        public string EntryName { get; set; }

        /// <summary>
        /// 本次请求的实际来源。
        /// </summary>
        public FlowTriggerSource Source { get; set; }

        /// <summary>
        /// 调用方提供的 Token；为空时运行时创建新 Token。
        /// </summary>
        public FlowToken Token { get; set; }

        /// <summary>
        /// 按入口输入协议提供的值，键不区分大小写。
        /// </summary>
        public IDictionary<string, object> Inputs { get; set; }
    }

    /// <summary>
    /// 一次流程运行的终态。
    /// </summary>
    public enum FlowRunStatus
    {
        Succeeded = 0,
        Failed = 1,
        Cancelled = 2,
        Rejected = 3
    }

    /// <summary>
    /// 统一流程运行结果，调用方无需通过异常推断正常、取消或拒绝状态。
    /// </summary>
    public sealed class FlowRunResult
    {
        public FlowRunResult()
        {
            Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 单次运行 ID，生命周期事件使用同一值关联。
        /// </summary>
        public string FlowRunId { get; set; }

        /// <summary>
        /// 本次运行使用的入口协议名。
        /// </summary>
        public string EntryName { get; set; }

        /// <summary>
        /// 本次运行的实际触发来源。
        /// </summary>
        public FlowTriggerSource Source { get; set; }

        /// <summary>
        /// 本次运行的 Token。
        /// </summary>
        public FlowToken Token { get; set; }

        /// <summary>
        /// 运行终态。
        /// </summary>
        public FlowRunStatus Status { get; set; }

        /// <summary>
        /// 失败、取消或拒绝原因；成功时为空。
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 开始执行或进入等待队列的 UTC 时间。
        /// </summary>
        public DateTime StartedAtUtc { get; set; }

        /// <summary>
        /// 到达终态的 UTC 时间。
        /// </summary>
        public DateTime CompletedAtUtc { get; set; }

        /// <summary>
        /// 本次运行结束时的变量池快照；被拒绝的运行保持为空。
        /// </summary>
        public IDictionary<string, object> Variables { get; set; }

        /// <summary>
        /// 是否成功完成。
        /// </summary>
        public bool IsSuccess
        {
            get { return Status == FlowRunStatus.Succeeded; }
        }
    }
}
