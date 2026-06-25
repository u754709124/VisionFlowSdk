using System.Collections.Generic;
using System.Linq;

namespace Vision.Flow.Core
{
    /// <summary>
    /// 流程校验问题严重级别。
    /// </summary>
    public enum FlowValidationSeverity
    {
        Error = 0,
        Warning = 1
    }

    /// <summary>
    /// 单条流程校验问题，包含稳定错误码和定位字段。
    /// </summary>
    public sealed class FlowValidationIssue
    {
        public FlowValidationSeverity Severity { get; set; }

        /// <summary>
        /// 稳定错误码，外部工具应优先依赖错误码而不是消息文本。
        /// </summary>
        public string Code { get; set; }

        public string Message { get; set; }

        public string NodeId { get; set; }

        public int? EdgeIndex { get; set; }

        public string EntryName { get; set; }

        /// <summary>
        /// 问题字段路径，通常对应运行态定义中的节点、连线、入口或设置位置。
        /// </summary>
        public string Field { get; set; }
    }

    /// <summary>
    /// 流程校验结果，聚合错误和警告并提供便捷查询。
    /// </summary>
    public sealed class FlowValidationResult
    {
        public FlowValidationResult()
        {
            Issues = new List<FlowValidationIssue>();
        }

        public List<FlowValidationIssue> Issues { get; private set; }

        public bool IsValid
        {
            get { return !Issues.Any(x => x.Severity == FlowValidationSeverity.Error); }
        }

        public IEnumerable<FlowValidationIssue> Errors
        {
            get { return Issues.Where(x => x.Severity == FlowValidationSeverity.Error); }
        }

        public IEnumerable<FlowValidationIssue> Warnings
        {
            get { return Issues.Where(x => x.Severity == FlowValidationSeverity.Warning); }
        }

        public void AddError(
            string code,
            string message,
            string nodeId = null,
            int? edgeIndex = null,
            string entryName = null,
            string field = null)
        {
            AddIssue(FlowValidationSeverity.Error, code, message, nodeId, edgeIndex, entryName, field);
        }

        public void AddWarning(
            string code,
            string message,
            string nodeId = null,
            int? edgeIndex = null,
            string entryName = null,
            string field = null)
        {
            AddIssue(FlowValidationSeverity.Warning, code, message, nodeId, edgeIndex, entryName, field);
        }

        public void AddIssue(
            FlowValidationSeverity severity,
            string code,
            string message,
            string nodeId = null,
            int? edgeIndex = null,
            string entryName = null,
            string field = null)
        {
            Issues.Add(new FlowValidationIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                NodeId = nodeId,
                EdgeIndex = edgeIndex,
                EntryName = entryName,
                Field = field
            });
        }
    }
}
